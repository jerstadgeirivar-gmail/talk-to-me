using System.IO;
using System.Media;

namespace TalkToMe.App;

internal static class VoiceCommandFeedback
{
    private const int SampleRate = 22_050;
    private static readonly byte[] ChimeWave = CreateChimeWave();

    public static void Play()
    {
        try
        {
            using MemoryStream stream = new(ChimeWave, writable: false);
            using SoundPlayer player = new(stream);
            player.PlaySync();
        }
        catch (InvalidOperationException)
        {
            // Audio feedback must never interrupt dictation.
        }
    }

    private static byte[] CreateChimeWave()
    {
        short[] samples =
        [
            .. CreateTone(frequency: 880, durationMilliseconds: 65),
            .. CreateTone(frequency: 1_175, durationMilliseconds: 95),
        ];
        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            int dataLength = samples.Length * sizeof(short);
            writer.Write("RIFF"u8);
            writer.Write(36 + dataLength);
            writer.Write("WAVE"u8);
            writer.Write("fmt "u8);
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(SampleRate);
            writer.Write(SampleRate * sizeof(short));
            writer.Write((short)sizeof(short));
            writer.Write((short)16);
            writer.Write("data"u8);
            writer.Write(dataLength);
            foreach (short sample in samples)
            {
                writer.Write(sample);
            }
        }

        return stream.ToArray();
    }

    private static short[] CreateTone(double frequency, int durationMilliseconds)
    {
        int sampleCount = SampleRate * durationMilliseconds / 1_000;
        short[] samples = new short[sampleCount];
        for (int index = 0; index < sampleCount; index++)
        {
            double position = (double)index / sampleCount;
            double attack = Math.Min(1, position / 0.08);
            double release = Math.Pow(1 - position, 2);
            double envelope = attack * release;
            double wave = Math.Sin(2 * Math.PI * frequency * index / SampleRate);
            samples[index] = (short)(wave * envelope * short.MaxValue * 0.24);
        }

        return samples;
    }
}
