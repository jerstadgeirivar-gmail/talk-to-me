using System.Diagnostics;
using System.Text;
using TalkToMe.Core;
using Whisper.net;

namespace TalkToMe.Infrastructure;

public sealed class LocalWhisperTranscriptionProvider : ITranscriptionProvider
{
    private readonly WhisperFactory _factory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LocalWhisperTranscriptionProvider(string modelPath)
    {
        try { _factory = WhisperFactory.FromPath(modelPath); }
        catch (Exception exception) when (exception is not TranscriptionException)
        {
            throw new TranscriptionException(TranscriptionFailureCategory.RuntimeUnavailable,
                "The local Whisper runtime or model could not be loaded. Repair the model and verify CPU compatibility.", innerException: exception);
        }
    }

    public async Task<TranscriptionResult> TranscribeAsync(RecordedAudio audio, TranscriptionContext context, CancellationToken cancellationToken)
    {
        if (!File.Exists(audio.FilePath)) throw new FileNotFoundException("Recorded audio was not found.", audio.FilePath);
        await _gate.WaitAsync(cancellationToken);
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            WhisperProcessorBuilder builder = _factory.CreateBuilder().WithLanguage(string.IsNullOrWhiteSpace(context.Language) ? "auto" : context.Language);
            if (!string.IsNullOrWhiteSpace(context.Prompt)) builder.WithPrompt(context.Prompt);
            using WhisperProcessor processor = builder.Build();
            await using FileStream stream = File.OpenRead(audio.FilePath);
            StringBuilder transcript = new();
            await foreach (SegmentData segment in processor.ProcessAsync(stream, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                transcript.Append(segment.Text);
            }
            string text = Normalize(transcript.ToString()).Trim();
            if (text.Length == 0) throw new TranscriptionException(TranscriptionFailureCategory.MalformedResponse, "Local Whisper produced an empty transcript.");
            return new(text, stopwatch.Elapsed, null);
        }
        catch (OutOfMemoryException exception)
        {
            throw new TranscriptionException(TranscriptionFailureCategory.InsufficientMemory, "There is not enough memory to run the local Whisper model.", innerException: exception);
        }
        catch (OperationCanceledException) { throw; }
        catch (TranscriptionException) { throw; }
        catch (Exception exception)
        {
            throw new TranscriptionException(TranscriptionFailureCategory.RuntimeUnavailable, "Local Whisper failed. The audio was retained for retry.", innerException: exception);
        }
        finally { stopwatch.Stop(); _gate.Release(); }
    }

    public ValueTask DisposeAsync()
    {
        _factory.Dispose();
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }

    private static string Normalize(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace("\r", "\n", StringComparison.Ordinal).Replace("\n", Environment.NewLine, StringComparison.Ordinal);
}
