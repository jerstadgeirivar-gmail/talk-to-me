using NAudio.Wave;
using TalkToMe.Core;

namespace TalkToMe.Infrastructure;

public static class WaveAudioTrimmer
{
    public static RecordingResult TrimEnd(RecordingResult recording, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero || !File.Exists(recording.FilePath))
        {
            return recording;
        }

        string temporaryPath = recording.FilePath + ".trim";
        try
        {
            long bytesWritten;
            WaveFormat format;
            using (WaveFileReader reader = new(recording.FilePath))
            {
                format = reader.WaveFormat;
                long bytesToTrim = (long)(duration.TotalSeconds * format.AverageBytesPerSecond);
                bytesToTrim -= bytesToTrim % format.BlockAlign;
                long bytesToKeep = Math.Max(0, reader.Length - bytesToTrim);
                byte[] buffer = new byte[16_384];
                bytesWritten = 0;
                using WaveFileWriter writer = new(temporaryPath, format);
                while (bytesWritten < bytesToKeep)
                {
                    int requested = (int)Math.Min(buffer.Length, bytesToKeep - bytesWritten);
                    int read = reader.Read(buffer, 0, requested);
                    if (read == 0)
                    {
                        break;
                    }

                    writer.Write(buffer, 0, read);
                    bytesWritten += read;
                }
            }

            File.Move(temporaryPath, recording.FilePath, overwrite: true);
            FileInfo file = new(recording.FilePath);
            return new RecordingResult(
                recording.FilePath,
                TimeSpan.FromSeconds((double)bytesWritten / format.AverageBytesPerSecond),
                file.Length);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
