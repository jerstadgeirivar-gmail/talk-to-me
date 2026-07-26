using NAudio.Wave;
using VoiceType.Core;

namespace VoiceType.Infrastructure;

public sealed class WaveAudioRecordingService : IAudioRecordingService
{
    private readonly object _syncRoot = new();
    private CancellationTokenSource? _recordingCancellation;
    private Task? _recordingTask;
    private string? _outputPath;
    private long _bytesWritten;

    public Task StartAsync(
        IAudioSource source,
        string outputPath,
        IProgress<RecordingProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Format != AudioFormat.SpeechPcm)
        {
            throw new ArgumentException("The audio source must emit the shared speech PCM format.", nameof(source));
        }

        lock (_syncRoot)
        {
            if (_recordingTask is not null)
            {
                throw new InvalidOperationException("A recording session is already active.");
            }

            string fullOutputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
            _recordingCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _outputPath = fullOutputPath;
            _bytesWritten = 0;
            _recordingTask = RecordAsync(source, fullOutputPath, progress, _recordingCancellation.Token);
        }

        return Task.CompletedTask;
    }

    public async Task<RecordingResult> StopAsync(CancellationToken cancellationToken)
    {
        Task recordingTask;
        CancellationTokenSource recordingCancellation;
        string outputPath;

        lock (_syncRoot)
        {
            recordingTask = _recordingTask
                ?? throw new InvalidOperationException("No recording session is active.");
            recordingCancellation = _recordingCancellation!;
            outputPath = _outputPath!;
            recordingCancellation.Cancel();
        }

        await recordingTask.WaitAsync(cancellationToken);
        FileInfo outputFile = new(outputPath);
        RecordingResult result = new(
            outputPath,
            TimeSpan.FromSeconds((double)_bytesWritten / AudioFormat.SpeechPcm.BytesPerSecond),
            outputFile.Length);

        lock (_syncRoot)
        {
            _recordingTask = null;
            _recordingCancellation = null;
            _outputPath = null;
        }

        recordingCancellation.Dispose();
        return result;
    }

    public async ValueTask DisposeAsync()
    {
        Task? recordingTask;
        CancellationTokenSource? recordingCancellation;
        lock (_syncRoot)
        {
            recordingTask = _recordingTask;
            recordingCancellation = _recordingCancellation;
        }

        if (recordingTask is not null && recordingCancellation is not null)
        {
            recordingCancellation.Cancel();
            await recordingTask;
            recordingCancellation.Dispose();
        }
    }

    private async Task RecordAsync(
        IAudioSource source,
        string outputPath,
        IProgress<RecordingProgress>? progress,
        CancellationToken cancellationToken)
    {
        WaveFormat waveFormat = new(
            source.Format.SampleRate,
            source.Format.BitsPerSample,
            source.Format.Channels);
        using WaveFileWriter writer = new(outputPath, waveFormat);

        try
        {
            await foreach (AudioFrame frame in source.ReadFramesAsync(cancellationToken))
            {
                writer.Write(frame.Data, 0, frame.Data.Length);
                writer.Flush();
                _bytesWritten += frame.Data.Length;
                progress?.Report(new RecordingProgress(
                    TimeSpan.FromSeconds((double)_bytesWritten / source.Format.BytesPerSecond),
                    _bytesWritten,
                    CalculatePeakLevel(frame.Data)));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static double CalculatePeakLevel(byte[] pcm16Data)
    {
        int peak = 0;
        for (int index = 0; index + 1 < pcm16Data.Length; index += 2)
        {
            short sample = BitConverter.ToInt16(pcm16Data, index);
            peak = Math.Max(peak, Math.Abs((int)sample));
        }

        return peak / (double)short.MaxValue;
    }
}
