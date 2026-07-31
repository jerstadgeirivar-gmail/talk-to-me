using System.Net;
using System.Speech.Synthesis;
using TalkToMe.Core;

namespace TalkToMe.Infrastructure;

public sealed class TranscriptionProviderRegistry(
    IApplicationSettingsStore settingsStore,
    INamedSecretStore secretStore) : ITranscriptionProviderFactory
{
    private static readonly IReadOnlyList<TranscriptionProviderDescriptor> Descriptors =
    [
        new(TranscriptionProviderIds.LocalWhisper, "Local Whisper", "Works offline; audio never leaves this computer.", true, true),
        new(TranscriptionProviderIds.AzureOpenAi, "Azure OpenAI", "Uses your Azure transcription deployment.", false, true),
        new(TranscriptionProviderIds.LmStudio, "LM Studio", "Requires a running local server; current official API has no speech-to-text endpoint.", false, false),
        new(TranscriptionProviderIds.Ollama, "Ollama", "Requires a running local server; current official API has no speech-to-text endpoint.", false, false),
    ];

    public IReadOnlyList<TranscriptionProviderDescriptor> Providers => Descriptors;

    public string SelectProviderId(ApplicationSettings settings, bool hasAzureSecret)
    {
        if (!string.IsNullOrWhiteSpace(settings.TranscriptionProviderId)) return settings.TranscriptionProviderId;
        return hasAzureSecret && !string.IsNullOrWhiteSpace(settings.AzureEndpoint) && !string.IsNullOrWhiteSpace(settings.AzureDeployment)
            ? TranscriptionProviderIds.AzureOpenAi
            : TranscriptionProviderIds.LocalWhisper;
    }

    public async Task<ITranscriptionProvider> CreateAsync(string providerId, CancellationToken cancellationToken)
    {
        ApplicationSettings settings = await settingsStore.LoadAsync(cancellationToken);
        return providerId switch
        {
            TranscriptionProviderIds.LocalWhisper => await CreateLocalAsync(cancellationToken),
            TranscriptionProviderIds.AzureOpenAi => await CreateAzureAsync(settings, cancellationToken),
            TranscriptionProviderIds.LmStudio => CreateUnsupported(settings.LmStudioBaseUrl, "LM Studio"),
            TranscriptionProviderIds.Ollama => CreateUnsupported(settings.OllamaBaseUrl, "Ollama"),
            _ => throw new TranscriptionException(TranscriptionFailureCategory.InvalidConfiguration,
                $"The selected transcription provider '{providerId}' is unknown or no longer installed. Open Settings and select a provider."),
        };
    }

    public async Task<ProviderTestResult> TestAsync(string providerId, CancellationToken cancellationToken)
    {
        ApplicationSettings settings = await settingsStore.LoadAsync(cancellationToken);
        try
        {
            return providerId switch
            {
                TranscriptionProviderIds.LocalWhisper => await LocalWhisperModel.VerifyAsync(cancellationToken),
                TranscriptionProviderIds.AzureOpenAi => await TestAzureAsync(settings, cancellationToken),
                TranscriptionProviderIds.LmStudio => await TestUnsupportedServerAsync(settings.LmStudioBaseUrl, "LM Studio", "/api/v1/models", cancellationToken),
                TranscriptionProviderIds.Ollama => await TestUnsupportedServerAsync(settings.OllamaBaseUrl, "Ollama", "/api/version", cancellationToken),
                _ => new(ProviderReadiness.InvalidConfiguration, $"Unknown provider '{providerId}'."),
            };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            return new(ProviderReadiness.Unavailable, exception.Message);
        }
    }

    private static async Task<ITranscriptionProvider> CreateLocalAsync(CancellationToken cancellationToken)
    {
        ProviderTestResult readiness = await LocalWhisperModel.VerifyAsync(cancellationToken);
        if (!readiness.IsReady)
        {
            TranscriptionFailureCategory category = readiness.Readiness == ProviderReadiness.ModelCorrupt
                ? TranscriptionFailureCategory.ModelCorrupt : TranscriptionFailureCategory.ModelMissing;
            throw new TranscriptionException(category, readiness.Message);
        }
        return new LocalWhisperTranscriptionProvider(LocalWhisperModel.ResolvePath());
    }

    private async Task<ITranscriptionProvider> CreateAzureAsync(ApplicationSettings settings, CancellationToken cancellationToken)
    {
        string? key = await secretStore.GetSecretAsync(TranscriptionProviderIds.AzureOpenAi, cancellationToken);
        AzureTranscriptionOptions? options;
        try { options = AzureTranscriptionOptions.FromConfiguration(settings, key); }
        catch (ArgumentException exception)
        {
            throw new TranscriptionException(TranscriptionFailureCategory.InvalidConfiguration, exception.Message, innerException: exception);
        }
        if (options is null)
        {
            throw new TranscriptionException(TranscriptionFailureCategory.MissingConfiguration,
                "Azure OpenAI is selected but endpoint, deployment, or API key is missing. Open Settings to complete Azure configuration.");
        }
        return new AzureTranscriptionProvider(options);
    }

    private async Task<ProviderTestResult> TestAzureAsync(ApplicationSettings settings, CancellationToken cancellationToken)
    {
        string? key = await secretStore.GetSecretAsync(TranscriptionProviderIds.AzureOpenAi, cancellationToken);
        AzureTranscriptionOptions? options;
        try
        {
            options = AzureTranscriptionOptions.FromConfiguration(settings, key);
        }
        catch (ArgumentException exception) { return new(ProviderReadiness.InvalidConfiguration, exception.Message); }

        if (options is null)
        {
            return new(ProviderReadiness.InvalidConfiguration, "Azure endpoint, deployment, and protected API key are required.");
        }

        string audioPath = Path.Combine(Path.GetTempPath(), $"talk-to-me-provider-test-{Guid.NewGuid():N}.wav");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (SpeechSynthesizer synthesizer = new())
            {
                synthesizer.SetOutputToWaveFile(audioPath);
                synthesizer.Speak("This is a Talk To Me transcription provider test.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            FileInfo audioFile = new(audioPath);
            await using AzureTranscriptionProvider provider = new(options with { Timeout = TimeSpan.FromSeconds(30) });
            TranscriptionResult result = await provider.TranscribeAsync(
                new RecordedAudio(audioPath, TimeSpan.Zero, audioFile.Length),
                new TranscriptionContext("en", "TalkToMe provider readiness test"),
                cancellationToken);
            return new(
                ProviderReadiness.Ready,
                $"Ready — Azure transcribed test audio in {result.ResponseTime.TotalSeconds:N1} seconds.");
        }
        finally
        {
            if (File.Exists(audioPath))
            {
                File.Delete(audioPath);
            }
        }
    }

    private static UnsupportedServerTranscriptionProvider CreateUnsupported(string baseUrl, string name)
    {
        ValidateServerUri(baseUrl, name);
        return new UnsupportedServerTranscriptionProvider(name);
    }

    private static async Task<ProviderTestResult> TestUnsupportedServerAsync(string baseUrl, string name, string path, CancellationToken cancellationToken)
    {
        Uri baseUri = ValidateServerUri(baseUrl, name);
        using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(10) };
        try
        {
            using HttpResponseMessage response = await client.GetAsync(new Uri(baseUri, path), cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new(ProviderReadiness.Configured, $"{name} is reachable but requires authentication. Its current documented API still has no speech-to-text endpoint.");
            if (!response.IsSuccessStatusCode)
                return new(ProviderReadiness.Unavailable, $"{name} responded with HTTP {(int)response.StatusCode}.");
            return new(ProviderReadiness.CapabilityUnavailable,
                $"{name} is reachable, but its current official API does not expose speech-to-text. Audio was not sent.");
        }
        catch (HttpRequestException)
        {
            return new(ProviderReadiness.Unavailable,
                $"{name} is not reachable at {baseUri}. Its current official API does not expose speech-to-text; no audio was sent. Start the server to test reachability.");
        }
    }

    public static Uri ValidateServerUri(string value, string name)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
            throw new TranscriptionException(TranscriptionFailureCategory.InvalidConfiguration, $"{name} base URL must be an absolute URL.");
        bool loopback = uri.IsLoopback;
        if (uri.Scheme != Uri.UriSchemeHttps && !(loopback && uri.Scheme == Uri.UriSchemeHttp))
            throw new TranscriptionException(TranscriptionFailureCategory.InvalidConfiguration,
                $"{name} must use HTTPS unless its host is localhost or a loopback address.");
        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
    }
}
