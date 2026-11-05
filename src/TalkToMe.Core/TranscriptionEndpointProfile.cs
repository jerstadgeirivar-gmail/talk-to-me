namespace TalkToMe.Core;

public sealed record TranscriptionEndpointProfile
{
    public const string LegacyId = "legacy-azure";

    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "New endpoint";
    public string Endpoint { get; init; } = string.Empty;
    public string Deployment { get; init; } = string.Empty;
    public string ApiVersion { get; init; } = "2025-04-01-preview";
    public bool OpenAiCompatibleRoute { get; init; }
    public bool BearerAuthentication { get; init; }
    public int MaximumOutputTokens { get; init; } = 2000;
    public int EstimatedTokensPerMinute { get; init; } = 600;
    public int MaximumSegmentSeconds { get; init; } = 300;
    public long MaximumUploadBytes { get; init; } = 24_000_000;
    public int RequestTimeoutSeconds { get; init; } = 600;
    public bool RetainProgress { get; init; }

    public string CredentialId => Id == LegacyId
        ? TranscriptionProviderIds.AzureOpenAi
        : $"{TranscriptionProviderIds.AzureOpenAi}-{Id}";

    public double SegmentSeconds => Math.Min(MaximumSegmentSeconds,
        MaximumOutputTokens * 0.75 / EstimatedTokensPerMinute * 60);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || Id.Length > 64 ||
            Id.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
            throw new ArgumentException("Endpoint profile ID is invalid.");
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 100)
            throw new ArgumentException("Enter an endpoint name of at most 100 characters.");
        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out Uri? uri) ||
            !(uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("Endpoint must be HTTPS (or loopback HTTP), without credentials, query or fragment.");
        if (string.IsNullOrWhiteSpace(Deployment)) throw new ArgumentException("Enter the deployment or model name.");
        if (!OpenAiCompatibleRoute && string.IsNullOrWhiteSpace(ApiVersion)) throw new ArgumentException("Enter the Azure API version.");
        if (MaximumOutputTokens is < 128 or > 1_000_000) throw new ArgumentException("Maximum output tokens must be between 128 and 1000000.");
        if (EstimatedTokensPerMinute is < 60 or > 10_000) throw new ArgumentException("Estimated output tokens per minute must be between 60 and 10000.");
        if (MaximumSegmentSeconds is < 10 or > 1800 || SegmentSeconds < 10)
            throw new ArgumentException("Segment duration must allow at least 10 seconds and be at most 1800 seconds.");
        if (MaximumUploadBytes is < 320_044 or > 25_000_000) throw new ArgumentException("Upload limit must be between 320044 and 25000000 bytes.");
        if (RequestTimeoutSeconds is < 10 or > 3600) throw new ArgumentException("Request timeout must be between 10 and 3600 seconds.");
    }
}