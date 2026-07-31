namespace TalkToMe.Core;

public enum TranscriptionFailureCategory
{
    MissingConfiguration,
    InvalidConfiguration,
    Authentication,
    Authorization,
    DeploymentNotFound,
    RequestTooLarge,
    RateLimited,
    UnsupportedMedia,
    InvalidRequest,
    Network,
    Timeout,
    MalformedResponse,
    CapabilityUnavailable,
    ModelMissing,
    ModelCorrupt,
    RuntimeUnavailable,
    InsufficientMemory,
    Server,
    Unknown,
}
