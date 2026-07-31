using System.Runtime.InteropServices;
using TalkToMe.Core;

namespace TalkToMe.Infrastructure;

public sealed class ClipboardTextInsertionService(
    IWindowTargetService windowTargetService,
    IClipboardAdapter clipboard,
    IKeyboardInputAdapter keyboardInput) : ITextInsertionService
{
    private const int ClipboardAttempts = 5;

    public async Task<TextInsertionResult> InsertAsync(
        WindowTarget target,
        string text,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        object? previousClipboard = await RetryClipboardAsync(
            clipboard.CaptureData,
            cancellationToken);
        await RetryClipboardAsync(
            () =>
            {
                clipboard.SetText(text);
                return true;
            },
            cancellationToken);

        try
        {
            if (!await windowTargetService.ActivateAsync(target, cancellationToken))
            {
                await RestorePreviousClipboardAsync(text, previousClipboard, cancellationToken);
                return new TextInsertionResult(
                    Inserted: false,
                    TranscriptLeftOnClipboard: false,
                    "The target window could not be activated. The previous clipboard contents were restored.");
            }

            keyboardInput.Paste();
            await Task.Delay(200, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await RestorePreviousClipboardAsync(text, previousClipboard, CancellationToken.None);
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await RestorePreviousClipboardAsync(text, previousClipboard, cancellationToken);
            return new TextInsertionResult(
                Inserted: false,
                TranscriptLeftOnClipboard: false,
                "Text insertion failed. The previous clipboard contents were restored.");
        }

        bool stillOwned = await RetryClipboardAsync(
            () => clipboard.ContainsText(text),
            cancellationToken);
        if (stillOwned)
        {
            await RetryClipboardAsync(
                () =>
                {
                    clipboard.RestoreData(previousClipboard);
                    return true;
                },
                cancellationToken);
        }

        return new TextInsertionResult(
            Inserted: true,
            TranscriptLeftOnClipboard: !stillOwned,
            stillOwned ? "Text inserted." : "Text inserted; another app changed the clipboard.");
    }

    private async Task RestorePreviousClipboardAsync(
        string text,
        object? previousClipboard,
        CancellationToken cancellationToken)
    {
        bool stillOwned = await RetryClipboardAsync(
            () => clipboard.ContainsText(text),
            cancellationToken);
        if (stillOwned)
        {
            await RetryClipboardAsync(
                () =>
                {
                    clipboard.RestoreData(previousClipboard);
                    return true;
                },
                cancellationToken);
        }
    }

    private static async Task<T> RetryClipboardAsync<T>(
        Func<T> action,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= ClipboardAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return action();
            }
            catch (COMException) when (attempt < ClipboardAttempts)
            {
                await Task.Delay(50 * attempt, cancellationToken);
            }
        }

        throw new InvalidOperationException("Clipboard retry policy exited unexpectedly.");
    }
}
