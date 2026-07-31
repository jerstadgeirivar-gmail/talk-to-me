using System.IO;
using System.Media;

namespace TalkToMe.App;

internal static class VoiceCommandFeedback
{
    private const int SampleRate = 22_050;
    private static readonly byte[] StartChimeWave = CreateChimeWave(
        firstFrequency: 880,
        secondFrequency: 1_175);
    private static readonly byte[] StopChimeWave = CreateChimeWave(
        firstFrequency: 1_175,
        secondFrequency: 880);

    public static void PlayStart() => Play(StartChimeWave);

    public static void PlayStop() => Play(StopChimeWave);

    private static void Play(byte[] chimeWave)
    {
        try
        {
            using MemoryStream stream = new(chimeWave, writable: false);
            using SoundPlayer player = new(stream);
            player.PlaySync();
        }
        catch (InvalidOperationException)
        {
            // Audio feedback must never interrupt dictation.
        }
    }

    private static byte[] CreateChimeWave(
        double firstFrequency,
        double secondFrequency)
    {
        short[] samples =
        [
            .. CreateTone(firstFrequency, durationMilliseconds: 65),
            .. CreateTone(secondFrequency, durationMilliseconds: 95),
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
