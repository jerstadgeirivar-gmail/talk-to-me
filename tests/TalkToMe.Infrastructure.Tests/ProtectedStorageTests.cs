using System.Text;
using TalkToMe.Core;

namespace TalkToMe.Infrastructure.Tests;

public sealed class ProtectedStorageTests
{
    [Fact]
    public async Task DpapiStoreProtectsRoundTripsAndRemovesSecret()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "credential.bin");
        const string syntheticSecret = "synthetic-test-secret-value";
        try
        {
            DpapiSecretStore store = new(path);
            await store.SetSecretAsync(syntheticSecret, CancellationToken.None);

            Assert.True(await store.HasSecretAsync(CancellationToken.None));
            Assert.Equal(syntheticSecret, await store.GetSecretAsync(CancellationToken.None));
            byte[] protectedBytes = await File.ReadAllBytesAsync(path, CancellationToken.None);
            Assert.DoesNotContain(syntheticSecret, Encoding.UTF8.GetString(protectedBytes));

            await store.RemoveSecretAsync(CancellationToken.None);
            Assert.False(await store.HasSecretAsync(CancellationToken.None));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task JsonStoreRoundTripsNonSecretSettingsAtomically()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "settings.json");
        try
        {
            JsonApplicationSettingsStore store = new(path);
            ApplicationSettings expected = new()
            {
                AzureEndpoint = "https://example.openai.azure.com/",
                AzureDeployment = "speech-deployment",
                TechnicalVocabulary = "Visual Studio Code",
                ClipboardOnlyMode = true,
            };

            await store.SaveAsync(expected, CancellationToken.None);
            ApplicationSettings actual = await store.LoadAsync(CancellationToken.None);

            Assert.Equal(expected, actual);
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LegacySettingsWithoutVoiceCommandPreferenceDefaultToDisabled()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "settings.json");
        try
        {
            await File.WriteAllTextAsync(
                path,
                """{"AzureEndpoint":"","AzureDeployment":"","Hotkey":"Win+<"}""");

            JsonApplicationSettingsStore store = new(path);
            ApplicationSettings actual = await store.LoadAsync(CancellationToken.None);

            Assert.Null(actual.VoiceCommandsEnabled);
            Assert.True(actual.VoiceCommandsEnabled is not true);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ExplicitNullStringsInLegacySettingsUseCurrentDefaults()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "settings.json");
        try
        {
            await File.WriteAllTextAsync(
                path,
                """
                {
                  "TranscriptionProviderId": "azure-openai",
                  "AzureEndpoint": "https://example.openai.azure.com",
                  "AzureDeployment": "speech-deployment",
                  "LocalWhisperModel": null,
                  "LmStudioBaseUrl": null,
                  "LmStudioModel": null,
                  "OllamaBaseUrl": null,
                  "OllamaModel": null
                }
                """);

            JsonApplicationSettingsStore store = new(path);
            ApplicationSettings actual = await store.LoadAsync(CancellationToken.None);

            Assert.Equal("small-q5_1", actual.LocalWhisperModel);
            Assert.Equal("http://localhost:1234", actual.LmStudioBaseUrl);
            Assert.Equal(string.Empty, actual.LmStudioModel);
            Assert.Equal("http://localhost:11434", actual.OllamaBaseUrl);
            Assert.Equal(string.Empty, actual.OllamaModel);
            Assert.Equal("https://example.openai.azure.com", actual.AzureEndpoint);
            Assert.Equal("speech-deployment", actual.AzureDeployment);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"talk-to-me-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
