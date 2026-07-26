namespace TalkToMe.Core;

public sealed record RecordedAudio(string FilePath, TimeSpan Duration, long FileSizeBytes);
