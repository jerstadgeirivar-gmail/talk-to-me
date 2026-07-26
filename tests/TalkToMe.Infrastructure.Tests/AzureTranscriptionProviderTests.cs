using System.Net;
using System.Net.Http.Headers;
using System.Text;
using TalkToMe.Core;

namespace TalkToMe.Infrastructure.Tests;

public sealed class AzureTranscriptionProviderTests
{
    [Fact]
    public async Task TranscribeAsyncSendsDocumentedV1RequestAndParsesText()
    {
        string audioPath = CreateAudioFile();
        try
        {
            bool requestWasInspected = false;
            using DelegateHandler handler = new(async (request, cancellationToken) =>
            {
                Assert.Equal(
                    "https://example.openai.azure.com/openai/v1/audio/transcriptions?api-version=v1",
                    request.RequestUri?.AbsoluteUri);
                Assert.Equal("test-key", request.Headers.GetValues("api-key").Single());
                MultipartFormDataContent multipart = Assert.IsType<MultipartFormDataContent>(request.Content);
                Dictionary<string, HttpContent> parts = multipart.ToDictionary(
                    part => part.Headers.ContentDisposition!.Name!.Trim('"'));
                Assert.Equal("speech-deployment", await parts["model"].ReadAsStringAsync(cancellationToken));
                Assert.Equal("json", await parts["response_format"].ReadAsStringAsync(cancellationToken));
                Assert.Equal("no", await parts["language"].ReadAsStringAsync(cancellationToken));
                Assert.Equal("audio/wav", parts["file"].Headers.ContentType?.MediaType);
                requestWasInspected = true;

                HttpResponseMessage response = new(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"text\":\"første linje\\nandre linje\"}",
                        Encoding.UTF8,
                        "application/json"),
                };
                response.Headers.Add("apim-request-id", "request-123");
                return response;
            });
            using HttpClient httpClient = new(handler);
            await using AzureTranscriptionProvider provider = new(CreateOptions(), httpClient);

            TranscriptionResult result = await provider.TranscribeAsync(
                new RecordedAudio(audioPath, TimeSpan.FromSeconds(1), new FileInfo(audioPath).Length),
                new TranscriptionContext("no", null),
                CancellationToken.None);

            Assert.True(requestWasInspected);
            Assert.Equal($"første linje{Environment.NewLine}andre linje", result.Text);
            Assert.Equal("request-123", result.RequestId);
        }
        finally
        {
            File.Delete(audioPath);
        }
    }

    [Fact]
    public async Task TranscribeAsyncUsesDeploymentRouteForFoundryServicesEndpoint()
    {
        string audioPath = CreateAudioFile();
        try
        {
            using DelegateHandler handler = new(async (request, cancellationToken) =>
            {
                Assert.Equal(
                    "https://example.services.ai.azure.com/openai/deployments/speech-deployment/audio/transcriptions?api-version=2025-04-01-preview",
                    request.RequestUri?.AbsoluteUri);
                MultipartFormDataContent multipart = Assert.IsType<MultipartFormDataContent>(request.Content);
                Dictionary<string, HttpContent> parts = multipart.ToDictionary(
                    part => part.Headers.ContentDisposition!.Name!.Trim('"'));
                Assert.Equal("speech-deployment", await parts["model"].ReadAsStringAsync(cancellationToken));
                Assert.Equal("json", await parts["response_format"].ReadAsStringAsync(cancellationToken));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"text\":\"test\"}", Encoding.UTF8, "application/json"),
                };
            });
            using HttpClient httpClient = new(handler);
            AzureTranscriptionOptions options = new(
                new Uri("https://example.services.ai.azure.com/"),
                "test-key",
                "speech-deployment",
                "2025-04-01-preview",
                TimeSpan.FromSeconds(10));
            await using AzureTranscriptionProvider provider = new(options, httpClient);

            TranscriptionResult result = await provider.TranscribeAsync(
                new RecordedAudio(audioPath, TimeSpan.FromSeconds(1), new FileInfo(audioPath).Length),
                new TranscriptionContext("no", null),
                CancellationToken.None);

            Assert.Equal("test", result.Text);
        }
        finally
        {
            File.Delete(audioPath);
        }
    }

    [Fact]
    public async Task TranscribeAsyncClassifiesRateLimitAndPreservesRetryAfter()
    {
        string audioPath = CreateAudioFile();
        try
        {
            using DelegateHandler handler = new((_, _) =>
            {
                HttpResponseMessage response = new(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(7));
                return Task.FromResult(response);
            });
            using HttpClient httpClient = new(handler);
            await using AzureTranscriptionProvider provider = new(CreateOptions(), httpClient);

            TranscriptionException exception = await Assert.ThrowsAsync<TranscriptionException>(() =>
                provider.TranscribeAsync(
                    new RecordedAudio(audioPath, TimeSpan.FromSeconds(1), new FileInfo(audioPath).Length),
                    new TranscriptionContext("no", null),
                    CancellationToken.None));

            Assert.Equal(TranscriptionFailureCategory.RateLimited, exception.Category);
            Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
            Assert.Equal(TimeSpan.FromSeconds(7), exception.RetryAfter);
        }
        finally
        {
            File.Delete(audioPath);
        }
    }

    private static AzureTranscriptionOptions CreateOptions() => new(
        new Uri("https://example.openai.azure.com/"),
        "test-key",
        "speech-deployment",
        "v1",
        TimeSpan.FromSeconds(10));

    private static string CreateAudioFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"talk-to-me-{Guid.NewGuid():N}.wav");
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes("RIFF0000WAVEtest-audio"));
        return path;
    }

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => sendAsync(request, cancellationToken);
    }
}
