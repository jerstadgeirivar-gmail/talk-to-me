using NAudio.Wave;
using TalkToMe.Core;
using TalkToMe.Infrastructure;

namespace TalkToMe.Infrastructure.Tests;

public sealed class WaveAudioTrimmerTests
{
    [Fact]
    public void TrimEndRemovesRequestedPcmDurationAndKeepsValidWaveFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"talk-to-me-trim-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "recording.wav");
        Directory.CreateDirectory(directory);
        try
        {
            WaveFormat format = new(16_000, 16, 1);
            byte[] twoSecondsOfAudio = new byte[format.AverageBytesPerSecond * 2];
            using (WaveFileWriter writer = new(path, format))
            {
                writer.Write(twoSecondsOfAudio, 0, twoSecondsOfAudio.Length);
            }

            RecordingResult original = new(
                path,
                TimeSpan.FromSeconds(2),
                new FileInfo(path).Length);
            RecordingResult trimmed = WaveAudioTrimmer.TrimEnd(
                original,
                TimeSpan.FromMilliseconds(750));

            Assert.Equal(TimeSpan.FromMilliseconds(1_250), trimmed.Duration);
            using WaveFileReader reader = new(path);
            Assert.Equal(format.AverageBytesPerSecond * 5 / 4, reader.Length);
            Assert.Equal(trimmed.FileSizeBytes, new FileInfo(path).Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TrimEndCanRemoveAnEntireShortRecording()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"talk-to-me-trim-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "recording.wav");
        Directory.CreateDirectory(directory);
        try
        {
            WaveFormat format = new(16_000, 16, 1);
            using (WaveFileWriter writer = new(path, format))
            {
                writer.Write(new byte[format.AverageBytesPerSecond / 4]);
            }

            RecordingResult trimmed = WaveAudioTrimmer.TrimEnd(
                new RecordingResult(path, TimeSpan.FromMilliseconds(250), new FileInfo(path).Length),
                TimeSpan.FromSeconds(1));

            Assert.Equal(TimeSpan.Zero, trimmed.Duration);
            using WaveFileReader reader = new(path);
            Assert.Equal(0, reader.Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
