using TalkToMe.Core;

namespace TalkToMe.Infrastructure.Tests;

public sealed class JsonApplicationSettingsStoreTests
{
    [Fact]
    public async Task MissingLanguageModeDefaultsToNorwegianAndRemainsStableAfterSaveAndReopen()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "settings.json");
        try
        {
            await File.WriteAllTextAsync(path, "{}");

            JsonApplicationSettingsStore store = new(path);
            ApplicationSettings normalized = await store.LoadAsync(CancellationToken.None);

            Assert.Equal(TranscriptionLanguageModes.Norwegian, normalized.TranscriptionLanguageMode);
            await store.SaveAsync(normalized, CancellationToken.None);

            ApplicationSettings reopened = await new JsonApplicationSettingsStore(path)
                .LoadAsync(CancellationToken.None);
            Assert.Equal(TranscriptionLanguageModes.Norwegian, reopened.TranscriptionLanguageMode);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("swedish")]
    public async Task NullOrUnknownLanguageModeDefaultsToNorwegianAndRemainsStableAfterSaveAndReopen(
        string? persistedLanguageMode)
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "settings.json");
        try
        {
            string jsonValue = persistedLanguageMode is null
                ? "null"
                : $"\"{persistedLanguageMode}\"";
            await File.WriteAllTextAsync(
                path,
                $"{{\"TranscriptionLanguageMode\":{jsonValue}}}");

            JsonApplicationSettingsStore store = new(path);
            ApplicationSettings normalized = await store.LoadAsync(CancellationToken.None);

            Assert.Equal(TranscriptionLanguageModes.Norwegian, normalized.TranscriptionLanguageMode);
            await store.SaveAsync(normalized, CancellationToken.None);

            ApplicationSettings reopened = await new JsonApplicationSettingsStore(path)
                .LoadAsync(CancellationToken.None);
            Assert.Equal(TranscriptionLanguageModes.Norwegian, reopened.TranscriptionLanguageMode);
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
