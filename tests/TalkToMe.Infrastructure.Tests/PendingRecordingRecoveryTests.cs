using System.Text;
using TalkToMe.Core;
using TalkToMe.Infrastructure;

namespace TalkToMe.Infrastructure.Tests;

public sealed class PendingRecordingRecoveryTests
{
    [Fact]
    public void RepairsInterruptedWaveAndRestoresTargetMetadata()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"talk-to-me-recovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "recording-20260727-100000.wav");

        try
        {
            WriteInterruptedWave(path, pcmBytes: AudioFormat.SpeechPcm.BytesPerSecond * 2);
            WindowTarget expectedTarget = new((nint)0x1234, 987, "Draft document");
            PendingRecordingRecovery.SaveTarget(path, expectedTarget);

            RecoveredRecording? recovered = PendingRecordingRecovery.FindLatest(directory);

            Assert.NotNull(recovered);
            Assert.Equal(TimeSpan.FromSeconds(2), recovered.Recording.Duration);
            Assert.Equal(expectedTarget, recovered.Target);
            using BinaryReader reader = new(File.OpenRead(path), Encoding.ASCII);
            Assert.Equal("RIFF", Encoding.ASCII.GetString(reader.ReadBytes(4)));
            Assert.Equal(new FileInfo(path).Length - 8, reader.ReadUInt32());
            reader.BaseStream.Position = 40;
            Assert.Equal((uint)(AudioFormat.SpeechPcm.BytesPerSecond * 2), reader.ReadUInt32());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void IgnoresFilesWithoutRecoverableAudio()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"talk-to-me-recovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllBytes(Path.Combine(directory, "recording-empty.wav"), new byte[44]);
            Assert.Null(PendingRecordingRecovery.FindLatest(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void WriteInterruptedWave(string path, int pcmBytes)
    {
        using BinaryWriter writer = new(File.Create(path), Encoding.ASCII);
        writer.Write("RIFF"u8);
        writer.Write(0u);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16u);
        writer.Write((ushort)1);
        writer.Write((ushort)AudioFormat.SpeechPcm.Channels);
        writer.Write((uint)AudioFormat.SpeechPcm.SampleRate);
        writer.Write((uint)AudioFormat.SpeechPcm.BytesPerSecond);
        writer.Write((ushort)2);
        writer.Write((ushort)AudioFormat.SpeechPcm.BitsPerSample);
        writer.Write("data"u8);
        writer.Write(0u);
        writer.Write(new byte[pcmBytes]);
    }
}
