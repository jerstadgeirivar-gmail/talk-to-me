using NAudio.MediaFoundation;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using TalkToMe.Core;

namespace TalkToMe.Infrastructure;

internal sealed class AzureAudioUploadPreparer
{
    private const int TargetSampleRate = 32_000;
    private const int TargetBitRate = 32_000;

    public static async Task<PreparedAzureAudio> PrepareAsync(
        RecordedAudio audio,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        FileInfo sourceFile = ValidateSource(audio);
        if (sourceFile.Length <= AzureTranscriptionOptions.PreparationThresholdBytes)
        {
            return PreparedAzureAudio.Original(audio.FilePath, sourceFile.Length);
        }

        progress?.Report("Compressing audio");
        string directory = Path.Combine(
            Path.GetDirectoryName(sourceFile.FullName)!,
            $".{sourceFile.Name}.azure-upload");
        Directory.CreateDirectory(directory);
        string outputPath = Path.Combine(directory, $"{Guid.NewGuid():N}.mp3");

        try
        {
            await Task.Run(
                () => EncodeMp3(sourceFile.FullName, outputPath, cancellationToken),
                CancellationToken.None);
            cancellationToken.ThrowIfCancellationRequested();

            FileInfo compressedFile = new(outputPath);
            if (!compressedFile.Exists || compressedFile.Length == 0)
            {
                throw new InvalidOperationException("Windows Media Foundation produced an empty audio artifact.");
            }

            return PreparedAzureAudio.Temporary(
                outputPath,
                compressedFile.Length,
                "audio/mpeg",
                Path.GetFileName(outputPath));
        }
        catch
        {
            DeleteArtifact(outputPath, directory);
            throw;
        }
    }

    private static FileInfo ValidateSource(RecordedAudio audio)
    {
        if (!File.Exists(audio.FilePath))
        {
            throw new FileNotFoundException("Recorded audio was not found.", audio.FilePath);
        }

        return new FileInfo(audio.FilePath);
    }

    private static void EncodeMp3(
        string sourcePath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        using WaveFileReader reader = new(sourcePath);
        ISampleProvider sampleProvider = reader.ToSampleProvider();
        if (sampleProvider.WaveFormat.Channels > 1)
        {
            sampleProvider = new StereoToMonoSampleProvider(sampleProvider);
        }

        if (sampleProvider.WaveFormat.SampleRate != TargetSampleRate)
        {
            sampleProvider = new WdlResamplingSampleProvider(sampleProvider, TargetSampleRate);
        }

        IWaveProvider waveProvider = new CancellationAwareWaveProvider(
            new SampleToWaveProvider16(sampleProvider),
            cancellationToken);
        int[] availableBitrates = MediaFoundationEncoder.GetEncodeBitrates(
            AudioSubtypes.MFAudioFormat_MP3,
            waveProvider.WaveFormat.SampleRate,
            waveProvider.WaveFormat.Channels);
        if (!availableBitrates.Contains(TargetBitRate))
        {
            throw new InvalidOperationException(
                $"Windows Media Foundation does not provide a mono MP3 encoder at {TargetBitRate / 1000} kbit/s for " +
                $"{waveProvider.WaveFormat.SampleRate / 1000} kHz audio. Available bitrates: " +
                string.Join(", ", availableBitrates.Select(value => $"{value / 1000} kbit/s")));
        }

        MediaFoundationEncoder.EncodeToMp3(waveProvider, outputPath, TargetBitRate);
    }

    private static void DeleteArtifact(string outputPath, string directory)
    {
        try
        {
            File.Delete(outputPath);
            if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
        catch
        {
        }
    }

    private sealed class CancellationAwareWaveProvider(
        IWaveProvider inner,
        CancellationToken cancellationToken) : IWaveProvider
    {
        public WaveFormat WaveFormat => inner.WaveFormat;

        public int Read(byte[] buffer, int offset, int count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return inner.Read(buffer, offset, count);
        }
    }
}

internal sealed class PreparedAzureAudio : IDisposable
{
    private readonly string? _temporaryDirectory;
    private bool _disposed;

    private PreparedAzureAudio(
        string filePath,
        long fileSizeBytes,
        string contentType,
        string fileName,
        string? temporaryDirectory)
    {
        FilePath = filePath;
        FileSizeBytes = fileSizeBytes;
        ContentType = contentType;
        FileName = fileName;
        _temporaryDirectory = temporaryDirectory;
    }

    public string FilePath { get; }

    public long FileSizeBytes { get; }

    public string ContentType { get; }

    public string FileName { get; }

    public static PreparedAzureAudio Original(string filePath, long fileSizeBytes) =>
        new(filePath, fileSizeBytes, "audio/wav", Path.GetFileName(filePath), null);

    public static PreparedAzureAudio Temporary(
        string filePath,
        long fileSizeBytes,
        string contentType,
        string fileName) =>
        new(filePath, fileSizeBytes, contentType, fileName, Path.GetDirectoryName(filePath));

    public void Dispose()
    {
        if (_disposed || _temporaryDirectory is null)
        {
            return;
        }

        _disposed = true;
        try
        {
            File.Delete(FilePath);
            if (Directory.Exists(_temporaryDirectory) &&
                !Directory.EnumerateFileSystemEntries(_temporaryDirectory).Any())
            {
                Directory.Delete(_temporaryDirectory);
            }
        }
        catch
        {
        }
    }
}