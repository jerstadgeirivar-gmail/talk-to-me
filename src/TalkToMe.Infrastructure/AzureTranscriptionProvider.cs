using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TalkToMe.Core;

namespace TalkToMe.Infrastructure;

public sealed partial class AzureTranscriptionProvider : ITranscriptionProvider
{
    private readonly AzureTranscriptionOptions _options;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public AzureTranscriptionProvider(AzureTranscriptionOptions options, HttpClient? httpClient = null)
    {
        _options = options;
        _httpClient = httpClient ?? new HttpClient();
        _ownsHttpClient = httpClient is null;
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
    }

    public Task<TranscriptionResult> TranscribeAsync(
        RecordedAudio audio,
        TranscriptionContext context,
        CancellationToken cancellationToken)
    {
        TranscriptionEndpointProfile profile = _options.Profile ?? new TranscriptionEndpointProfile
        {
            Id = TranscriptionEndpointProfile.LegacyId,
            Endpoint = _options.Endpoint.AbsoluteUri,
            Deployment = _options.Deployment,
            ApiVersion = _options.ApiVersion,
        };
        return new AudioSegmentTranscriber(profile).TranscribeAsync(audio, context, TranscribeSingleAsync, cancellationToken);
    }

    private async Task<TranscriptionResult> TranscribeSingleAsync(
        RecordedAudio audio,
        TranscriptionContext context,
        CancellationToken cancellationToken)
    {
        ValidateAudio(audio);
        long uploadBytes = new FileInfo(audio.FilePath).Length;
        long maximumBytes = _options.Profile?.MaximumUploadBytes ?? 24_000_000;
        if (uploadBytes > maximumBytes)
            throw new TranscriptionException(TranscriptionFailureCategory.RequestTooLarge, "The audio segment exceeds the endpoint's configured upload limit.");
        string contentType = Path.GetExtension(audio.FilePath).ToLowerInvariant() switch
        {
            ".mp3" or ".mpga" or ".mpeg" => "audio/mpeg",
            ".m4a" or ".mp4" or ".m4b" => "audio/mp4",
            ".webm" => "audio/webm",
            ".flac" => "audio/flac",
            _ => "audio/wav",
        };

        context.Progress?.Report("Sending audio");
        Uri requestUri = BuildRequestUri();
        using MultipartFormDataContent content = new();
        await using FileStream audioStream = File.OpenRead(audio.FilePath);
        using StreamContent fileContent = new(audioStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", Path.GetFileName(audio.FilePath));
        content.Add(new StringContent(_options.Deployment), "model");
        content.Add(new StringContent("json"), "response_format");
        if (!string.IsNullOrWhiteSpace(context.Language))
        {
            content.Add(new StringContent(context.Language), "language");
        }

        if (!string.IsNullOrWhiteSpace(context.Prompt))
        {
            content.Add(new StringContent(context.Prompt), "prompt");
        }

        using HttpRequestMessage request = new(HttpMethod.Post, requestUri) { Content = content };
        if (_options.Profile?.BearerAuthentication is true)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        else
            request.Headers.Add("api-key", _options.ApiKey);
        using CancellationTokenSource timeoutCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(_options.Timeout);
        Stopwatch stopwatch = Stopwatch.StartNew();
        RuntimeDiagnostics.Write("azure.request.started", new { deployment = _options.Deployment, apiVersion = _options.ApiVersion, uploadBytes, contentType, timeoutSeconds = _options.Timeout.TotalSeconds });

        try
        {
            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCancellation.Token);
            context.Progress?.Report("Waiting for transcription");
            string? requestId = GetRequestId(response);
            RuntimeDiagnostics.Write("azure.response.received", new { statusCode = (int)response.StatusCode, requestId, elapsedSeconds = stopwatch.Elapsed.TotalSeconds });
            if (!response.IsSuccessStatusCode)
            {
                throw await CreateResponseExceptionAsync(response, timeoutCancellation.Token);
            }

            AzureTranscriptionResponse? payload = await response.Content.ReadFromJsonAsync(
                AzureTranscriptionJsonContext.Default.AzureTranscriptionResponse,
                timeoutCancellation.Token);
            if (string.IsNullOrWhiteSpace(payload?.Text))
            {
                throw new TranscriptionException(
                    TranscriptionFailureCategory.MalformedResponse,
                    "Azure returned a transcription response without text.");
            }

            if (_options.Profile is { } profile && payload.Usage?.OutputTokens >= profile.MaximumOutputTokens)
                throw new TranscriptionException(TranscriptionFailureCategory.OutputLimitExceeded,
                    "The transcription reached the endpoint's configured output-token limit.");

                    stopwatch.Stop();
                    RuntimeDiagnostics.Write("azure.request.completed", new { requestId, elapsedSeconds = stopwatch.Elapsed.TotalSeconds, characters = payload.Text.Length, outputTokens = payload.Usage?.OutputTokens });
            return new TranscriptionResult(
                NormalizeLineEndings(payload.Text),
                stopwatch.Elapsed,
                requestId);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            RuntimeDiagnostics.Write("azure.request.timeout", new { elapsedSeconds = stopwatch.Elapsed.TotalSeconds });
            throw new TranscriptionException(
                TranscriptionFailureCategory.Timeout,
                "Azure transcription timed out. The audio was retained for retry.",
                innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            throw new TranscriptionException(
                TranscriptionFailureCategory.Network,
                "Azure transcription could not be reached. The audio was retained for retry.",
                innerException: exception);
        }
        catch (JsonException exception)
        {
            throw new TranscriptionException(
                TranscriptionFailureCategory.MalformedResponse,
                "Azure returned an unreadable transcription response.",
                innerException: exception);
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private static void ValidateAudio(RecordedAudio audio)
    {
        if (!File.Exists(audio.FilePath))
        {
            throw new FileNotFoundException("Recorded audio was not found.", audio.FilePath);
        }

    }

    private Uri BuildRequestUri()
    {
        Uri baseUri = new(_options.Endpoint.AbsoluteUri.TrimEnd('/') + "/");
        if (_options.Profile?.OpenAiCompatibleRoute is true)
            return new Uri(baseUri, "audio/transcriptions");
        string relativePath = UsesV1Route()
            ? $"openai/v1/audio/transcriptions?api-version={Uri.EscapeDataString(_options.ApiVersion)}"
            : $"openai/deployments/{Uri.EscapeDataString(_options.Deployment)}/audio/transcriptions" +
              $"?api-version={Uri.EscapeDataString(_options.ApiVersion)}";
        return new Uri(baseUri, relativePath);
    }

    private bool UsesV1Route() =>
        _options.ApiVersion.Equals("v1", StringComparison.OrdinalIgnoreCase);

    private async Task<TranscriptionException> CreateResponseExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        TranscriptionFailureCategory category = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => TranscriptionFailureCategory.InvalidRequest,
            HttpStatusCode.Unauthorized => TranscriptionFailureCategory.Authentication,
            HttpStatusCode.Forbidden => TranscriptionFailureCategory.Authorization,
            HttpStatusCode.NotFound => TranscriptionFailureCategory.DeploymentNotFound,
            HttpStatusCode.RequestEntityTooLarge => TranscriptionFailureCategory.RequestTooLarge,
            HttpStatusCode.UnsupportedMediaType => TranscriptionFailureCategory.UnsupportedMedia,
            HttpStatusCode.TooManyRequests => TranscriptionFailureCategory.RateLimited,
            >= HttpStatusCode.InternalServerError => TranscriptionFailureCategory.Server,
            _ => TranscriptionFailureCategory.Unknown,
        };
        TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta ??
            (response.Headers.RetryAfter?.Date is { } retryDate ? retryDate - DateTimeOffset.UtcNow : null);
        string message = category switch
        {
            TranscriptionFailureCategory.Authentication => "Azure rejected the configured API key.",
            TranscriptionFailureCategory.Authorization => "The credential is not authorized for this deployment.",
            TranscriptionFailureCategory.DeploymentNotFound => "The configured transcription deployment was not found.",
            TranscriptionFailureCategory.RequestTooLarge => "The recording exceeds Azure's request limit.",
            TranscriptionFailureCategory.RateLimited => "Azure is rate limiting transcription. Retry later.",
            TranscriptionFailureCategory.UnsupportedMedia => "Azure rejected the recording format.",
            TranscriptionFailureCategory.InvalidRequest => "Azure rejected the transcription request.",
            TranscriptionFailureCategory.Server => "Azure transcription is temporarily unavailable.",
            _ => $"Azure transcription failed with HTTP {(int)response.StatusCode}.",
        };
        string? errorCode = null;
        try
        {
            using StreamReader reader = new(await response.Content.ReadAsStreamAsync(cancellationToken));
            char[] buffer = new char[8192];
            int count = await reader.ReadBlockAsync(buffer.AsMemory(), cancellationToken);
            using JsonDocument document = JsonDocument.Parse(new string(buffer, 0, count));
            if (document.RootElement.TryGetProperty("error", out JsonElement error) &&
                error.ValueKind == JsonValueKind.Object &&
                error.TryGetProperty("code", out JsonElement code) && code.ValueKind == JsonValueKind.String)
            {
                string value = code.GetString() ?? string.Empty;
                if (value.Length <= 128 && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.'))
                    errorCode = value.Replace(_options.ApiKey, "[redacted]", StringComparison.Ordinal);
            }
        }
        catch (JsonException)
        {
        }
        string? requestId = GetRequestId(response);
        if (errorCode is "context_length_exceeded" or "max_tokens_exceeded" or "audio_too_long")
            category = TranscriptionFailureCategory.OutputLimitExceeded;
        RuntimeDiagnostics.Write("azure.request.rejected", new { category = category.ToString(), statusCode = (int)response.StatusCode, requestId, errorCode, retryAfterSeconds = retryAfter?.TotalSeconds });
        string details = $" HTTP {(int)response.StatusCode}" + (errorCode is null ? "." : $" ({errorCode}).");
        if (requestId is not null) details += $" Request ID: {requestId}.";
        return new TranscriptionException(category, message + details, response.StatusCode, retryAfter);
    }

    private static string? GetRequestId(HttpResponseMessage response)
    {
        foreach (string headerName in new[] { "x-request-id", "apim-request-id" })
        {
            if (response.Headers.TryGetValues(headerName, out IEnumerable<string>? values))
            {
                return values.FirstOrDefault();
            }
        }

        return null;
    }

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\n", Environment.NewLine, StringComparison.Ordinal);

    private sealed record AzureTranscriptionResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("text")] string Text,
        [property: System.Text.Json.Serialization.JsonPropertyName("usage")] AzureTranscriptionUsage? Usage);

    private sealed record AzureTranscriptionUsage(
        [property: System.Text.Json.Serialization.JsonPropertyName("output_tokens")] int? OutputTokens);

    [System.Text.Json.Serialization.JsonSerializable(typeof(AzureTranscriptionResponse))]
    private sealed partial class AzureTranscriptionJsonContext :
        System.Text.Json.Serialization.JsonSerializerContext;
}
