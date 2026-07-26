namespace TalkToMe.Core;

public interface IAudioRecordingService : IAsyncDisposable
{
    Task StartAsync(
        IAudioSource source,
        string outputPath,
        IProgress<RecordingProgress>? progress,
        CancellationToken cancellationToken);

    Task<RecordingResult> StopAsync(CancellationToken cancellationToken);
}
