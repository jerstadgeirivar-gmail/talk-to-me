using TalkToMe.Core;

namespace TalkToMe.Infrastructure;

public sealed record AzureTranscriptionOptions(
    Uri Endpoint,
    string ApiKey,
    string Deployment,
    string ApiVersion,
    TimeSpan Timeout)
{
    public const long MaximumAudioBytes = 25 * 1024 * 1024;

    public static AzureTranscriptionOptions? FromEnvironment()
    {
        return FromConfiguration(new ApplicationSettings(), storedApiKey: null);
    }

    public static AzureTranscriptionOptions? FromConfiguration(
        ApplicationSettings settings,
        string? storedApiKey)
    {
        string? endpoint = Environment.GetEnvironmentVariable("TALKTOME_AZURE_ENDPOINT") ?? settings.AzureEndpoint;
        string? apiKey = Environment.GetEnvironmentVariable("TALKTOME_AZURE_API_KEY") ?? storedApiKey;
        string? deployment =
            Environment.GetEnvironmentVariable("TALKTOME_AZURE_DEPLOYMENT") ?? settings.AzureDeployment;
        string apiVersion =
            Environment.GetEnvironmentVariable("TALKTOME_AZURE_API_VERSION") ?? settings.AzureApiVersion;

        if (string.IsNullOrWhiteSpace(endpoint) ||
            string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(deployment))
        {
            return null;
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? endpointUri) ||
            endpointUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("TALKTOME_AZURE_ENDPOINT must be an absolute HTTPS URI.");
        }

        return new AzureTranscriptionOptions(
            endpointUri,
            apiKey,
            deployment,
            apiVersion,
            TimeSpan.FromMinutes(10));
    }
}
