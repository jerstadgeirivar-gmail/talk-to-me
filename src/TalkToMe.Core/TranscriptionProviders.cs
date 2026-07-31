namespace TalkToMe.Core;

public static class TranscriptionProviderIds
{
    public const string LocalWhisper = "local-whisper";
    public const string AzureOpenAi = "azure-openai";
    public const string LmStudio = "lm-studio";
    public const string Ollama = "ollama";
}

public sealed record TranscriptionProviderDescriptor(
    string Id,
    string DisplayName,
    string Description,
    bool WorksOffline,
    bool SupportsTranscription);

public enum ProviderReadiness
{
    Ready,
    Configured,
    Unavailable,
    CapabilityUnavailable,
    ModelMissing,
    ModelCorrupt,
    InvalidConfiguration,
}

public sealed record ProviderTestResult(ProviderReadiness Readiness, string Message)
{
    public bool IsReady => Readiness == ProviderReadiness.Ready;
}

public interface ITranscriptionProviderFactory
{
    IReadOnlyList<TranscriptionProviderDescriptor> Providers { get; }
    string SelectProviderId(ApplicationSettings settings, bool hasAzureSecret);
    Task<ITranscriptionProvider> CreateAsync(string providerId, CancellationToken cancellationToken);
    Task<ProviderTestResult> TestAsync(string providerId, CancellationToken cancellationToken);
}
