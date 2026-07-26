namespace VoiceType.Core;

public sealed record RecordingResult(string FilePath, TimeSpan Duration, long FileSizeBytes);
