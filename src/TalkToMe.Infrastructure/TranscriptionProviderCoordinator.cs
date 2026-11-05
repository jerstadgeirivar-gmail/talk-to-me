using TalkToMe.Core;

namespace TalkToMe.Infrastructure;

public sealed class TranscriptionProviderCoordinator(ITranscriptionProviderFactory factory, IApplicationSettingsStore settingsStore, string providerId) : ITranscriptionProvider
{
    private const string NorwegianEnglishPrompt =
        "Talen er hovedsakelig norsk, men kan inneholde engelske ord, tekniske begreper, identifikatorer og setninger.";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ITranscriptionProvider? _provider;
    private volatile string _providerId = providerId;
    private int _selectionVersion;
    private int _loadedSelectionVersion = -1;

    public string ProviderId => _providerId;

    public Task ApplySelectionAsync(string providerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _providerId = providerId;
        Interlocked.Increment(ref _selectionVersion);
        return Task.CompletedTask;
    }

    public async Task<TranscriptionResult> TranscribeAsync(RecordedAudio audio, TranscriptionContext context, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        RuntimeDiagnostics.Write("transcription.started", new { provider = _providerId, audio.Duration, audio.FileSizeBytes });
        try
        {
            int selectionVersion = Volatile.Read(ref _selectionVersion);
            string selectedProvider = _providerId;
            if (_loadedSelectionVersion != selectionVersion || _provider is null)
            {
                if (_provider is not null) await _provider.DisposeAsync();
                _provider = null;
                _provider = await factory.CreateAsync(selectedProvider, cancellationToken);
                _loadedSelectionVersion = selectionVersion;
            }
            ApplicationSettings settings = await settingsStore.LoadAsync(cancellationToken);
            try
            {
                settings = settings with { TechnicalVocabulary = await settingsStore.LoadVocabularyAsync(cancellationToken) };
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.DecoderFallbackException)
            {
                throw new TranscriptionException(TranscriptionFailureCategory.RuntimeUnavailable,
                    $"Cannot read vocabulary file '{settingsStore.VocabularyFilePath}'. Save it as UTF-8 and check file access, then retry.",
                    innerException: exception);
            }
            TranscriptionContext effectiveContext = CreateEffectiveContext(context, settings);
            TranscriptionResult result = await _provider.TranscribeAsync(audio, effectiveContext, cancellationToken);
            RuntimeDiagnostics.Write("transcription.completed", new { provider = _providerId, characters = result.Text.Length });
            return result;
        }
        catch (Exception exception)
        {
            RuntimeDiagnostics.Failure("transcription.failed", exception);
            throw;
        }
        finally { _gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try { if (_provider is not null) await _provider.DisposeAsync(); }
        finally { _gate.Release(); _gate.Dispose(); }
    }

    private static TranscriptionContext CreateEffectiveContext(
        TranscriptionContext context,
        ApplicationSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(context.Language))
        {
            return string.IsNullOrWhiteSpace(context.Prompt)
                ? context with { Prompt = NullIfWhiteSpace(settings.TechnicalVocabulary) }
                : context;
        }

        string? language = settings.TranscriptionLanguageMode == TranscriptionLanguageModes.Auto
            ? null
            : "no";
        string? languagePrompt = settings.TranscriptionLanguageMode == TranscriptionLanguageModes.NorwegianEnglish
            ? NorwegianEnglishPrompt
            : null;
        return context with
        {
            Language = language,
            Prompt = JoinPrompt(languagePrompt, settings.TechnicalVocabulary),
        };
    }

    private static string? JoinPrompt(string? first, string? second)
    {
        string? normalizedFirst = NullIfWhiteSpace(first);
        string? normalizedSecond = NullIfWhiteSpace(second);
        if (normalizedFirst is null) return normalizedSecond;
        if (normalizedSecond is null) return normalizedFirst;
        return $"{normalizedFirst} {normalizedSecond}";
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
