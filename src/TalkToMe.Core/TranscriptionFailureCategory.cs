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
    Server,
    Unknown,
}
