using TalkToMe.Core;

namespace TalkToMe.Infrastructure;

public sealed class UnsupportedServerTranscriptionProvider(string displayName) : ITranscriptionProvider
{
    public Task<TranscriptionResult> TranscribeAsync(RecordedAudio audio, TranscriptionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new TranscriptionException(TranscriptionFailureCategory.CapabilityUnavailable,
            $"{displayName} does not expose a documented speech-to-text endpoint. Select Local Whisper or Azure OpenAI, or configure this profile only when a future documented audio API is available.");
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
