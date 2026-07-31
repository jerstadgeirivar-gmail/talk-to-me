using TalkToMe.Core;

namespace TalkToMe.Infrastructure;

public sealed class TranscriptionProviderCoordinator(ITranscriptionProviderFactory factory, IApplicationSettingsStore settingsStore, string providerId) : ITranscriptionProvider
{
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
            TranscriptionContext effectiveContext = string.IsNullOrWhiteSpace(context.Prompt)
                ? context with { Prompt = settings.TechnicalVocabulary }
                : context;
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
}
