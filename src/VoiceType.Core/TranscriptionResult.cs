namespace VoiceType.Core;

public sealed record TranscriptionResult(string Text, TimeSpan ResponseTime, string? RequestId);
