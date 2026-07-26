namespace VoiceType.Core;

public interface ITextInsertionService
{
    Task<TextInsertionResult> InsertAsync(
        WindowTarget target,
        string text,
        CancellationToken cancellationToken);
}
