using System.Net;

namespace VoiceType.Core;

public sealed class TranscriptionException : Exception
{
    public TranscriptionException(
        TranscriptionFailureCategory category,
        string message,
        HttpStatusCode? statusCode = null,
        TimeSpan? retryAfter = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Category = category;
        StatusCode = statusCode;
        RetryAfter = retryAfter;
    }

    public TranscriptionFailureCategory Category { get; }

    public HttpStatusCode? StatusCode { get; }

    public TimeSpan? RetryAfter { get; }
}
