namespace TalkToMe.Core;

public sealed record RecordingProgress(TimeSpan Duration, long BytesWritten, double PeakLevel);
