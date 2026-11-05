using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using TalkToMe.Core;

namespace TalkToMe.Infrastructure;

internal sealed class AudioSegmentTranscriber(TranscriptionEndpointProfile profile)
{
    private const int BytesPerSecond = 32_000;
    private const int OverlapBytes = BytesPerSecond;

    public Task<TranscriptionResult> TranscribeAsync(RecordedAudio audio, TranscriptionContext context,
        Func<RecordedAudio, TranscriptionContext, CancellationToken, Task<TranscriptionResult>> transcribe,
        CancellationToken cancellationToken) => Task.Run(async () =>
    {
        profile.Validate();
        Stopwatch elapsed = Stopwatch.StartNew();
        using NormalizedAudioReader reader = new(audio.FilePath);
        long sourceBytes = new FileInfo(audio.FilePath).Length;
        if (sourceBytes <= profile.MaximumUploadBytes && reader.Duration.TotalSeconds <= profile.SegmentSeconds)
        {
            try { return await transcribe(audio with { Duration = reader.Duration, FileSizeBytes = sourceBytes }, context, cancellationToken); }
            catch (TranscriptionException exception) when (exception.Category == TranscriptionFailureCategory.OutputLimitExceeded)
            {
                RuntimeDiagnostics.Write("segmentation.required", new { reason = "output-limit", audioSeconds = reader.Duration.TotalSeconds });
            }
        }

        int budget = (int)Math.Min(profile.MaximumUploadBytes - 4096,
            Math.Floor(profile.SegmentSeconds * BytesPerSecond));
        budget -= budget % 2;
        string directory = Path.Combine(Path.GetDirectoryName(RuntimeDiagnostics.LogPath)!, "TranscriptionJobs", profile.Id);
        Directory.CreateDirectory(directory);
        await using FileStream source = File.OpenRead(audio.FilePath);
        byte[] sourceHash = await SHA256.HashDataAsync(source, cancellationToken);
        string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            source = Convert.ToHexString(sourceHash), profile, context.Language, context.Prompt,
        }))));
        string checkpointPath = Path.Combine(directory, fingerprint + ".bin");
        List<CompletedSegment> completed;
        try { completed = profile.RetainProgress ? await LoadCheckpointAsync(checkpointPath, cancellationToken) : []; }
        catch (Exception exception) when (exception is CryptographicException or JsonException or InvalidDataException)
        {
            throw new TranscriptionException(TranscriptionFailureCategory.RuntimeUnavailable,
                "Saved transcription progress cannot be read. Clear saved progress for this endpoint in Settings, then retry.", innerException: exception);
        }
        CleanExpiredArtifacts(directory);
        string workingDirectory = Path.Combine(directory, $".audio-{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDirectory);
        RuntimeDiagnostics.Write("segmentation.started", new
        {
            profile.Id, profile.MaximumOutputTokens, profile.EstimatedTokensPerMinute,
            profile.MaximumUploadBytes, segmentSeconds = profile.SegmentSeconds,
            audioSeconds = reader.Duration.TotalSeconds, sourceBytes, resumedSegments = completed.Count,
        });
        bool successful = false;
        try
        {
            byte[] buffer = new byte[budget];
            int carried = 0;
            long startByte = 0;
            int segmentIndex = 0;
            StringBuilder transcript = new();
            TranscriptionResult? last = null;
            bool previousOverlap = false;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int count = carried;
                bool endOfAudio = false;
                while (count < buffer.Length)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int read = reader.Read(buffer, count, Math.Min(65_536, buffer.Length - count));
                    if (read == 0) { endOfAudio = true; break; }
                    count += read;
                }
                if (count == 0) break;
                if (count % 2 != 0) throw new InvalidDataException("Decoded PCM audio ended in an incomplete sample.");
                (int length, bool silence) = FindBoundary(buffer, count, endOfAudio);
                segmentIndex++;
                double percentage = Math.Min(100, startByte / (double)BytesPerSecond / reader.Duration.TotalSeconds * 100);
                TranscriptionContext segmentContext = BuildContext(context, transcript.ToString()) with
                {
                    Progress = new SegmentProgress(context.Progress, segmentIndex, percentage),
                };
                segmentContext.Progress.Report("Transcribing");
                last = await ProcessAsync(buffer, 0, length, startByte, segmentContext, 0);
                AppendTranscript(transcript, last.Text, previousOverlap);
                int overlap = !endOfAudio && !silence ? OverlapBytes : 0;
                int consumed = length - overlap;
                carried = count - consumed;
                Buffer.BlockCopy(buffer, consumed, buffer, 0, carried);
                startByte += consumed;
                previousOverlap = overlap > 0;
                if (endOfAudio && carried == 0) break;
            }
            if (string.IsNullOrWhiteSpace(transcript.ToString()))
                throw new TranscriptionException(TranscriptionFailureCategory.MalformedResponse, "The segmented recording produced no transcript.");
            successful = true;
            RuntimeDiagnostics.Write("segmentation.completed", new { segments = completed.Count, characters = transcript.Length, elapsedSeconds = elapsed.Elapsed.TotalSeconds });
            return new TranscriptionResult(transcript.ToString().Trim(), elapsed.Elapsed, last?.RequestId);

            async Task<TranscriptionResult> ProcessAsync(byte[] samples, int offset, int length, long absoluteStart,
                TranscriptionContext segmentContext, int depth)
            {
                CompletedSegment? saved = completed.FirstOrDefault(entry => entry.StartByte == absoluteStart && entry.Length == length);
                if (saved is not null)
                {
                    RuntimeDiagnostics.Write("segment.resumed", new { startSeconds = absoluteStart / (double)BytesPerSecond, durationSeconds = length / (double)BytesPerSecond, saved.RequestId });
                    return new TranscriptionResult(saved.Text, TimeSpan.Zero, saved.RequestId);
                }
                bool hasChildren = completed.Any(entry => entry.StartByte >= absoluteStart && entry.StartByte + entry.Length <= absoluteStart + length);
                if (!hasChildren)
                {
                    string path = Path.Combine(workingDirectory, Guid.NewGuid().ToString("N") + ".wav");
                    try
                    {
                        using (WaveFileWriter writer = new(path, new WaveFormat(16_000, 16, 1))) writer.Write(samples, offset, length);
                        TranscriptionResult result = await SendWithRetryAsync(new RecordedAudio(path, TimeSpan.FromSeconds(length / (double)BytesPerSecond), new FileInfo(path).Length), segmentContext);
                        completed.Add(new CompletedSegment(absoluteStart, length, result.Text, result.RequestId));
                        if (profile.RetainProgress) await SaveCheckpointAsync(checkpointPath, completed, cancellationToken);
                        RuntimeDiagnostics.Write("segment.completed", new { startSeconds = absoluteStart / (double)BytesPerSecond, durationSeconds = length / (double)BytesPerSecond, result.RequestId });
                        return result;
                    }
                    catch (TranscriptionException exception) when (exception.Category == TranscriptionFailureCategory.OutputLimitExceeded)
                    {
                        RuntimeDiagnostics.Write("segment.reducing", new { startSeconds = absoluteStart / (double)BytesPerSecond, durationSeconds = length / (double)BytesPerSecond });
                    }
                    finally { File.Delete(path); }
                }
                if (depth >= 8 || length < 20 * BytesPerSecond)
                    throw new TranscriptionException(TranscriptionFailureCategory.OutputLimitExceeded,
                        "A short audio segment still exceeds the configured model token budget. Check endpoint limits in Settings and retry.");
                int midpoint = length / 2;
                midpoint -= midpoint % 2;
                TranscriptionResult first = await ProcessAsync(samples, offset, midpoint, absoluteStart, segmentContext, depth + 1);
                int secondOffset = midpoint - OverlapBytes;
                TranscriptionResult second = await ProcessAsync(samples, offset + secondOffset, length - secondOffset,
                    absoluteStart + secondOffset, BuildContext(context, first.Text), depth + 1);
                StringBuilder joined = new(first.Text);
                AppendTranscript(joined, second.Text, true);
                return new TranscriptionResult(joined.ToString(), first.ResponseTime + second.ResponseTime, second.RequestId);
            }

            async Task<TranscriptionResult> SendWithRetryAsync(RecordedAudio segment, TranscriptionContext segmentContext)
            {
                for (int attempt = 0; ; attempt++)
                {
                    try { return await transcribe(segment, segmentContext, cancellationToken); }
                    catch (TranscriptionException exception) when (attempt < 2 && exception.Category is
                        TranscriptionFailureCategory.RateLimited or TranscriptionFailureCategory.Server or TranscriptionFailureCategory.Network or TranscriptionFailureCategory.Timeout)
                    {
                        TimeSpan delay = exception.RetryAfter ?? TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
                        if (delay > TimeSpan.FromMinutes(2)) throw;
                        context.Progress?.Report($"Waiting to retry audio segment ({delay.TotalSeconds:N0}s)");
                        RuntimeDiagnostics.Write("segment.retry", new { attempt = attempt + 1, category = exception.Category.ToString(), delaySeconds = delay.TotalSeconds });
                        await Task.Delay(delay < TimeSpan.Zero ? TimeSpan.Zero : delay, cancellationToken);
                    }
                }
            }
        }
        finally
        {
            Directory.Delete(workingDirectory, true);
            if (successful || !profile.RetainProgress) File.Delete(checkpointPath);
        }
    }, cancellationToken);

    private static (int Length, bool Silence) FindBoundary(byte[] buffer, int count, bool endOfAudio)
    {
        if (endOfAudio) return (count, true);
        int window = BytesPerSecond / 5;
        int searchStart = Math.Max(count / 2, count - BytesPerSecond * 10);
        for (int end = count; end - window >= searchStart; end -= window / 2)
        {
            long energy = 0;
            int peak = 0;
            for (int position = end - window; position < end; position += 2)
            {
                int sample = Math.Abs((int)System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(position, 2)));
                energy += sample;
                peak = Math.Max(peak, sample);
            }
            if (peak < 1500 && energy / (window / 2) < 250) return (end - window / 2, true);
        }
        return (count, false);
    }

    private static void CleanExpiredArtifacts(string directory)
    {
        foreach (string file in Directory.EnumerateFiles(directory, "*.bin"))
            if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-14)) File.Delete(file);
        foreach (string artifact in Directory.EnumerateDirectories(directory, ".audio-*"))
        {
            string[] parts = Path.GetFileName(artifact).Split('-');
            if (parts.Length < 3 || !int.TryParse(parts[1], out int processId)) continue;
            try { using Process owner = Process.GetProcessById(processId); if (!owner.HasExited) continue; }
            catch (ArgumentException) { }
            Directory.Delete(artifact, true);
        }
    }

    private static TranscriptionContext BuildContext(TranscriptionContext context, string previous)
    {
        if (previous.Length == 0) return context;
        string tail = previous.Length > 512 ? previous[^512..] : previous;
        return context with { Prompt = (context.Prompt ?? string.Empty) + "\nPrevious transcript context: " + tail };
    }

    private static void AppendTranscript(StringBuilder destination, string next, bool overlap)
    {
        string text = next.Trim();
        if (overlap && destination.Length > 0)
        {
            string[] previousWords = destination.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            string[] nextWords = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            for (int length = Math.Min(20, Math.Min(previousWords.Length, nextWords.Length)); length >= 3; length--)
            {
                bool matches = true;
                for (int index = 0; index < length; index++)
                {
                    string previousWord = new(previousWords[previousWords.Length - length + index].Where(char.IsLetterOrDigit).ToArray());
                    string nextWord = new(nextWords[index].Where(char.IsLetterOrDigit).ToArray());
                    if (previousWord.Length == 0 || !previousWord.Equals(nextWord, StringComparison.OrdinalIgnoreCase)) { matches = false; break; }
                }
                if (matches) { text = string.Join(' ', nextWords.Skip(length)); break; }
            }
        }
        if (text.Length == 0) return;
        if (destination.Length > 0) destination.AppendLine();
        destination.Append(text);
    }

    private static async Task<List<CompletedSegment>> LoadCheckpointAsync(string path, CancellationToken token)
    {
        if (!File.Exists(path)) return [];
        if (new FileInfo(path).Length > 10 * 1024 * 1024) throw new InvalidDataException("Saved transcription progress exceeds its size limit.");
        byte[] encrypted = await File.ReadAllBytesAsync(path, token);
        byte[] plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
        try
        {
            List<CompletedSegment> entries = JsonSerializer.Deserialize<List<CompletedSegment>>(plain)
                ?? throw new InvalidDataException("Saved transcription progress is unreadable.");
            if (entries.Any(entry => entry.StartByte < 0 || entry.Length <= 0 || string.IsNullOrWhiteSpace(entry.Text)))
                throw new InvalidDataException("Saved transcription progress contains invalid segments.");
            return entries;
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    private static async Task SaveCheckpointAsync(string path, List<CompletedSegment> entries, CancellationToken token)
    {
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(entries);
        try
        {
            byte[] encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            string temporary = path + ".tmp";
            try
            {
                await File.WriteAllBytesAsync(temporary, encrypted, token);
                File.Move(temporary, path, true);
            }
            finally { File.Delete(temporary); }
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    private sealed record CompletedSegment(long StartByte, int Length, string Text, string? RequestId);

    private sealed class SegmentProgress(IProgress<string>? inner, int index, double percentage) : IProgress<string>
    {
        public void Report(string value) => inner?.Report($"{value} (segment {index}, {percentage:N0}% of audio)");
    }

    private sealed class NormalizedAudioReader : IDisposable
    {
        private readonly IDisposable _reader;
        private readonly IWaveProvider _provider;

        public NormalizedAudioReader(string path)
        {
            if (Path.GetExtension(path).Equals(".wav", StringComparison.OrdinalIgnoreCase))
            {
                WaveFileReader wave = new(path);
                if (wave.WaveFormat.Encoding == WaveFormatEncoding.Pcm && wave.WaveFormat.SampleRate == 16_000 &&
                    wave.WaveFormat.Channels == 1 && wave.WaveFormat.BitsPerSample == 16)
                {
                    _reader = wave; _provider = wave; Duration = wave.TotalTime;
                    return;
                }
                wave.Dispose();
            }
            AudioFileReader decoded = new(path);
            _reader = decoded;
            Duration = decoded.TotalTime;
            ISampleProvider samples = decoded;
            if (samples.WaveFormat.Channels == 2) samples = new StereoToMonoSampleProvider(samples);
            if (samples.WaveFormat.Channels != 1)
            {
                decoded.Dispose();
                throw new TranscriptionException(TranscriptionFailureCategory.UnsupportedMedia, "Import mono or stereo audio; multichannel transcription is not supported.");
            }
            if (samples.WaveFormat.SampleRate != 16_000) samples = new WdlResamplingSampleProvider(samples, 16_000);
            _provider = new SampleToWaveProvider16(samples);
        }

        public TimeSpan Duration { get; }
        public int Read(byte[] buffer, int offset, int count) => _provider.Read(buffer, offset, count);
        public void Dispose() => _reader.Dispose();
    }
}