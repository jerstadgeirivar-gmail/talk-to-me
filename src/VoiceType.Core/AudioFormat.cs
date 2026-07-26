namespace VoiceType.Core;

public sealed record AudioFormat(int SampleRate, int BitsPerSample, int Channels)
{
    public static AudioFormat SpeechPcm { get; } = new(16_000, 16, 1);

    public int BytesPerSecond => SampleRate * (BitsPerSample / 8) * Channels;
}
