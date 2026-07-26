namespace TalkToMe.Core;

public sealed record AudioFrame(byte[] Data, TimeSpan Position);
