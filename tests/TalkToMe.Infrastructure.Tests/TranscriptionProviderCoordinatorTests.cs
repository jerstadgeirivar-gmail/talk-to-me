using TalkToMe.Core;

namespace TalkToMe.Infrastructure.Tests;

public sealed class TranscriptionProviderCoordinatorTests
{
    [Fact]
    public async Task AutoModeUsesAutomaticLanguageAndTrimmedTechnicalVocabulary()
    {
        RecordingProvider provider = new();
        InMemorySettingsStore settingsStore = new(new ApplicationSettings
        {
            TranscriptionLanguageMode = TranscriptionLanguageModes.Auto,
            TechnicalVocabulary = "  GitHub Actions  ",
        });
        StubProviderFactory factory = new(provider);
        await using TranscriptionProviderCoordinator coordinator = new(
            factory,
            settingsStore,
            TranscriptionProviderIds.LocalWhisper);

        await coordinator.TranscribeAsync(CreateAudio(), new(null, null), CancellationToken.None);

        Assert.Equal(new TranscriptionContext(null, "GitHub Actions"), Assert.Single(provider.Contexts));
    }

    [Fact]
    public async Task NorwegianModeUsesNorwegianLanguageAndOmitsBlankTechnicalVocabulary()
    {
        RecordingProvider provider = new();
        InMemorySettingsStore settingsStore = new(new ApplicationSettings
        {
            TranscriptionLanguageMode = TranscriptionLanguageModes.Norwegian,
            TechnicalVocabulary = "  ",
        });
        await using TranscriptionProviderCoordinator coordinator = new(
            new StubProviderFactory(provider),
            settingsStore,
            TranscriptionProviderIds.LocalWhisper);

        await coordinator.TranscribeAsync(CreateAudio(), new(null, null), CancellationToken.None);

        Assert.Equal(new TranscriptionContext("no", null), Assert.Single(provider.Contexts));
    }

    [Fact]
    public async Task NorwegianEnglishModeCombinesBilingualPromptAndTechnicalVocabulary()
    {
        RecordingProvider provider = new();
        InMemorySettingsStore settingsStore = new(new ApplicationSettings
        {
            TranscriptionLanguageMode = TranscriptionLanguageModes.NorwegianEnglish,
            TechnicalVocabulary = "  C# .NET  ",
        });
        await using TranscriptionProviderCoordinator coordinator = new(
            new StubProviderFactory(provider),
            settingsStore,
            TranscriptionProviderIds.LocalWhisper);

        await coordinator.TranscribeAsync(CreateAudio(), new(null, null), CancellationToken.None);

        Assert.Equal(
            new TranscriptionContext(
                "no",
                "Talen er hovedsakelig norsk, men kan inneholde engelske ord, tekniske begreper, identifikatorer og setninger. C# .NET"),
            Assert.Single(provider.Contexts));
    }

    [Fact]
    public async Task ExplicitLanguageAndPromptArePreserved()
    {
        RecordingProvider provider = new();
        InMemorySettingsStore settingsStore = new(new ApplicationSettings
        {
            TranscriptionLanguageMode = TranscriptionLanguageModes.NorwegianEnglish,
            TechnicalVocabulary = "Ignored technical vocabulary",
        });
        await using TranscriptionProviderCoordinator coordinator = new(
            new StubProviderFactory(provider),
            settingsStore,
            TranscriptionProviderIds.LocalWhisper);
        TranscriptionContext explicitContext = new("en", "  Explicit provider context  ");

        await coordinator.TranscribeAsync(CreateAudio(), explicitContext, CancellationToken.None);

        Assert.Equal(explicitContext, Assert.Single(provider.Contexts));
    }

    [Fact]
    public async Task RetryReusesProviderAndAppliesCurrentSettings()
    {
        RecordingProvider provider = new(new InvalidOperationException("first attempt failed"));
        InMemorySettingsStore settingsStore = new(new ApplicationSettings
        {
            TranscriptionLanguageMode = TranscriptionLanguageModes.Norwegian,
        });
        StubProviderFactory factory = new(provider);
        await using TranscriptionProviderCoordinator coordinator = new(
            factory,
            settingsStore,
            TranscriptionProviderIds.LocalWhisper);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.TranscribeAsync(CreateAudio(), new(null, null), CancellationToken.None));

        settingsStore.Settings = settingsStore.Settings with
        {
            TranscriptionLanguageMode = TranscriptionLanguageModes.Auto,
            TechnicalVocabulary = " retry term ",
        };
        TranscriptionResult result = await coordinator.TranscribeAsync(
            CreateAudio(),
            new(null, null),
            CancellationToken.None);

        Assert.Equal("test transcript", result.Text);
        Assert.Equal(1, factory.CreateCount);
        Assert.Equal(
            [
                new TranscriptionContext("no", null),
                new TranscriptionContext(null, "retry term"),
            ],
            provider.Contexts);
    }

    private static RecordedAudio CreateAudio() =>
        new("synthetic.wav", TimeSpan.FromSeconds(1), 1);

    private sealed class InMemorySettingsStore(ApplicationSettings settings) : IApplicationSettingsStore
    {
        public ApplicationSettings Settings { get; set; } = settings;

        public Task<ApplicationSettings> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Settings);
        }

        public Task SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class StubProviderFactory(RecordingProvider provider) : ITranscriptionProviderFactory
    {
        public IReadOnlyList<TranscriptionProviderDescriptor> Providers { get; } = [];

        public int CreateCount { get; private set; }

        public string SelectProviderId(ApplicationSettings settings, bool hasAzureSecret) =>
            TranscriptionProviderIds.LocalWhisper;

        public Task<ITranscriptionProvider> CreateAsync(
            string providerId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CreateCount++;
            return Task.FromResult<ITranscriptionProvider>(provider);
        }

        public Task<ProviderTestResult> TestAsync(
            string providerId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingProvider(params Exception?[] failures) : ITranscriptionProvider
    {
        private readonly Queue<Exception?> _failures = new(failures);

        public List<TranscriptionContext> Contexts { get; } = [];

        public Task<TranscriptionResult> TranscribeAsync(
            RecordedAudio audio,
            TranscriptionContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Contexts.Add(context);
            if (_failures.Count > 0 && _failures.Dequeue() is Exception failure)
            {
                return Task.FromException<TranscriptionResult>(failure);
            }

            return Task.FromResult(new TranscriptionResult(
                "test transcript",
                TimeSpan.Zero,
                null));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
