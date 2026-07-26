using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using TalkToMe.Core;

namespace TalkToMe.Infrastructure;

public static class PendingRecordingRecovery
{
    private const int MinimumWaveLength = 44;
    private const string TargetMetadataSuffix = ".target.json";

    public static RecoveredRecording? FindLatest(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        foreach (string path in Directory
                     .EnumerateFiles(directory, "recording-*.wav")
                     .OrderByDescending(File.GetLastWriteTimeUtc))
        {
            RecordingResult? recording = TryRepair(path);
            if (recording is not null)
            {
                return new RecoveredRecording(recording, TryLoadTarget(path));
            }
        }

        return null;
    }

    public static void SaveTarget(string audioPath, WindowTarget? target)
    {
        string metadataPath = GetMetadataPath(audioPath);
        if (target is null)
        {
            File.Delete(metadataPath);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(metadataPath)!);
        TargetMetadata metadata = new(target.Handle, target.ProcessId, target.Title);
        string json = JsonSerializer.Serialize(metadata);
        File.WriteAllText(metadataPath + ".tmp", json, Encoding.UTF8);
        File.Move(metadataPath + ".tmp", metadataPath, overwrite: true);
    }

    public static void Delete(string audioPath)
    {
        File.Delete(audioPath);
        File.Delete(GetMetadataPath(audioPath));
    }

    public static RecordingResult? TryRepair(string path)
    {
        FileInfo file = new(path);
        if (!file.Exists || file.Length < MinimumWaveLength)
        {
            return null;
        }

        using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.Read);
        Span<byte> header = stackalloc byte[12];
        if (stream.Read(header) != header.Length ||
            !header[..4].SequenceEqual("RIFF"u8) ||
            !header[8..12].SequenceEqual("WAVE"u8))
        {
            return null;
        }

        long dataSizeOffset = FindDataSizeOffset(stream);
        if (dataSizeOffset < 0)
        {
            return null;
        }

        long dataOffset = dataSizeOffset + sizeof(uint);
        long dataLength = stream.Length - dataOffset;
        int blockAlign = AudioFormat.SpeechPcm.Channels * (AudioFormat.SpeechPcm.BitsPerSample / 8);
        dataLength -= dataLength % blockAlign;
        if (dataLength <= 0)
        {
            return null;
        }

        if (stream.Length != dataOffset + dataLength)
        {
            stream.SetLength(dataOffset + dataLength);
        }

        Span<byte> value = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(value, checked((uint)(stream.Length - 8)));
        stream.Position = 4;
        stream.Write(value);
        BinaryPrimitives.WriteUInt32LittleEndian(value, checked((uint)dataLength));
        stream.Position = dataSizeOffset;
        stream.Write(value);
        stream.Flush(flushToDisk: true);

        return new RecordingResult(
            path,
            TimeSpan.FromSeconds((double)dataLength / AudioFormat.SpeechPcm.BytesPerSecond),
            stream.Length);
    }

    private static long FindDataSizeOffset(Stream stream)
    {
        stream.Position = 12;
        Span<byte> chunkHeader = stackalloc byte[8];
        while (stream.Position + chunkHeader.Length <= stream.Length)
        {
            if (stream.Read(chunkHeader) != chunkHeader.Length)
            {
                return -1;
            }

            uint chunkLength = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader[4..]);
            if (chunkHeader[..4].SequenceEqual("data"u8))
            {
                return stream.Position - sizeof(uint);
            }

            long nextChunk = stream.Position + chunkLength + (chunkLength % 2);
            if (nextChunk > stream.Length)
            {
                return -1;
            }

            stream.Position = nextChunk;
        }

        return -1;
    }

    private static WindowTarget? TryLoadTarget(string audioPath)
    {
        string metadataPath = GetMetadataPath(audioPath);
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        try
        {
            TargetMetadata? metadata = JsonSerializer.Deserialize<TargetMetadata>(
                File.ReadAllText(metadataPath));
            return metadata is null
                ? null
                : new WindowTarget((nint)metadata.Handle, metadata.ProcessId, metadata.Title);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string GetMetadataPath(string audioPath) => audioPath + TargetMetadataSuffix;

    private sealed record TargetMetadata(long Handle, int ProcessId, string Title);
}
