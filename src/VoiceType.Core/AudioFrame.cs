namespace VoiceType.Core;

public sealed record AudioFrame(byte[] Data, TimeSpan Position);
