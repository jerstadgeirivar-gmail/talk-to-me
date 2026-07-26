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

    public async Task<TranscriptionResult> TranscribeAsync(
        RecordedAudio audio,
        TranscriptionContext context,
        CancellationToken cancellationToken)
    {
        ValidateAudio(audio);
        Uri requestUri = BuildRequestUri();
        using MultipartFormDataContent content = new();
        await using FileStream audioStream = File.OpenRead(audio.FilePath);
        using StreamContent fileContent = new(audioStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(fileContent, "file", Path.GetFileName(audio.FilePath));
        if (UsesV1Route())
        {
            content.Add(new StringContent(_options.Deployment), "model");
        }

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
        request.Headers.Add("api-key", _options.ApiKey);
        using CancellationTokenSource timeoutCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(_options.Timeout);
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCancellation.Token);
            stopwatch.Stop();
            string? requestId = GetRequestId(response);
            if (!response.IsSuccessStatusCode)
            {
                throw CreateResponseException(response);
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

            return new TranscriptionResult(
                NormalizeLineEndings(payload.Text),
                stopwatch.Elapsed,
                requestId);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
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

        if (audio.FileSizeBytes > AzureTranscriptionOptions.MaximumAudioBytes)
        {
            throw new TranscriptionException(
                TranscriptionFailureCategory.RequestTooLarge,
                "The recording exceeds Azure's 25 MB request limit.");
        }
    }

    private Uri BuildRequestUri()
    {
        Uri baseUri = new(_options.Endpoint.AbsoluteUri.TrimEnd('/') + "/");
        string relativePath = UsesV1Route()
            ? $"openai/v1/audio/transcriptions?api-version={Uri.EscapeDataString(_options.ApiVersion)}"
            : $"openai/deployments/{Uri.EscapeDataString(_options.Deployment)}/audio/transcriptions" +
              $"?api-version={Uri.EscapeDataString(_options.ApiVersion)}";
        return new Uri(baseUri, relativePath);
    }

    private bool UsesV1Route() =>
        _options.ApiVersion.Equals("preview", StringComparison.OrdinalIgnoreCase) ||
        _options.ApiVersion.Equals("v1", StringComparison.OrdinalIgnoreCase);

    private static TranscriptionException CreateResponseException(HttpResponseMessage response)
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
        TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta;
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
        return new TranscriptionException(category, message, response.StatusCode, retryAfter);
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
        [property: System.Text.Json.Serialization.JsonPropertyName("text")] string Text);

    [System.Text.Json.Serialization.JsonSerializable(typeof(AzureTranscriptionResponse))]
    private sealed partial class AzureTranscriptionJsonContext :
        System.Text.Json.Serialization.JsonSerializerContext;
}
