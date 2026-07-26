namespace VoiceType.Core;

public sealed record RecordingProgress(TimeSpan Duration, long BytesWritten, double PeakLevel);
