using System.Diagnostics;
using TalkToMe.Core;

namespace TalkToMe.Infrastructure;

public sealed class DiagnosticTranscriptionProvider(string transcript) : ITranscriptionProvider
{
    public async Task<TranscriptionResult> TranscribeAsync(
        RecordedAudio audio,
        TranscriptionContext context,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(audio.FilePath) || audio.FileSizeBytes <= 44)
        {
            throw new InvalidOperationException("Diagnostic transcription requires finalized recorded audio.");
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        await Task.Delay(250, cancellationToken);
        return new TranscriptionResult(transcript, stopwatch.Elapsed, "diagnostic");
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
