using TalkToMe.Core;

namespace TalkToMe.Infrastructure;

public sealed record AzureTranscriptionOptions(
    Uri Endpoint,
    string ApiKey,
    string Deployment,
    string ApiVersion,
    TimeSpan Timeout)
{
    public TranscriptionEndpointProfile? Profile { get; init; }

    public const long PreparationThresholdBytes = 24 * 1024 * 1024;
    public const long MaximumAudioBytes = 25 * 1024 * 1024;

    public static AzureTranscriptionOptions? FromEnvironment()
    {
        return FromConfiguration(new ApplicationSettings(), storedApiKey: null);
    }

    public static AzureTranscriptionOptions? FromConfiguration(
        ApplicationSettings settings,
        string? storedApiKey)
    {
        TranscriptionEndpointProfile profile = ResolveProfile(settings);
        string? endpoint = Environment.GetEnvironmentVariable("TALKTOME_AZURE_ENDPOINT") ?? profile.Endpoint;
        string? apiKey = Environment.GetEnvironmentVariable("TALKTOME_AZURE_API_KEY") ?? storedApiKey;
        string? deployment =
            Environment.GetEnvironmentVariable("TALKTOME_AZURE_DEPLOYMENT") ?? profile.Deployment;
        string apiVersion =
            Environment.GetEnvironmentVariable("TALKTOME_AZURE_API_VERSION") ?? profile.ApiVersion;

        if (string.IsNullOrWhiteSpace(endpoint) ||
            string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(deployment))
        {
            return null;
        }

        profile = profile with { Endpoint = endpoint, Deployment = deployment, ApiVersion = apiVersion };
        profile.Validate();
        Uri endpointUri = new(endpoint);

        return new AzureTranscriptionOptions(
            endpointUri,
            apiKey,
            deployment,
            apiVersion,
            TimeSpan.FromSeconds(profile.RequestTimeoutSeconds)) { Profile = profile };
    }

    public static TranscriptionEndpointProfile ResolveProfile(ApplicationSettings settings)
    {
        if (settings.TranscriptionEndpoints.Count == 0)
            return new TranscriptionEndpointProfile
            {
                Id = TranscriptionEndpointProfile.LegacyId,
                Name = "Existing Azure endpoint",
                Endpoint = settings.AzureEndpoint,
                Deployment = settings.AzureDeployment,
                ApiVersion = settings.AzureApiVersion,
            };
        return settings.TranscriptionEndpoints.FirstOrDefault(profile => profile.Id == settings.SelectedTranscriptionEndpointId)
            ?? throw new ArgumentException("The selected endpoint profile was not found. Select an endpoint in Settings.");
    }
}
