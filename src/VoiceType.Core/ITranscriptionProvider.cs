namespace VoiceType.Core;

public interface ITranscriptionProvider : IAsyncDisposable
{
    Task<TranscriptionResult> TranscribeAsync(
        RecordedAudio audio,
        TranscriptionContext context,
        CancellationToken cancellationToken);
}
