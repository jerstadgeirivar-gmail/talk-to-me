using TalkToMe.Core;

namespace TalkToMe.Infrastructure;

public sealed class TranscriptionProviderCoordinator(ITranscriptionProviderFactory factory, IApplicationSettingsStore settingsStore, string providerId) : ITranscriptionProvider
{
    private const string NorwegianEnglishPrompt =
        "Talen er hovedsakelig norsk, men kan inneholde engelske ord, tekniske begreper, identifikatorer og setninger.";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ITranscriptionProvider? _provider;
    private string _providerId = providerId;
    private bool _reload = true;

    public string ProviderId => _providerId;

    public async Task ApplySelectionAsync(string providerId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_providerId != providerId) _providerId = providerId;
            _reload = true;
            if (_provider is not null)
            {
                await _provider.DisposeAsync();
                _provider = null;
            }
        }
        finally { _gate.Release(); }
    }

    public async Task<TranscriptionResult> TranscribeAsync(RecordedAudio audio, TranscriptionContext context, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_reload || _provider is null)
            {
                if (_provider is not null) await _provider.DisposeAsync();
                _provider = await factory.CreateAsync(_providerId, cancellationToken);
                _reload = false;
            }
            ApplicationSettings settings = await settingsStore.LoadAsync(cancellationToken);
            TranscriptionContext effectiveContext = CreateEffectiveContext(context, settings);
            return await _provider.TranscribeAsync(audio, effectiveContext, cancellationToken);
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
