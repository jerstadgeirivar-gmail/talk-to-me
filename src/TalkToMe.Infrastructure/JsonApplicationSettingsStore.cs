using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TalkToMe.Core;

namespace TalkToMe.Infrastructure;

public sealed partial class JsonApplicationSettingsStore(string? settingsFile = null) : IApplicationSettingsStore
{
    private readonly string _settingsFile = settingsFile ?? TalkToMeDataPaths.SettingsFile;
    public string VocabularyFilePath { get; } = Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(settingsFile ?? TalkToMeDataPaths.SettingsFile))!,
        "vocabulary.txt");

    public async Task<ApplicationSettings> LoadAsync(CancellationToken cancellationToken)
    {
        ApplicationSettings settings = new();
        if (File.Exists(_settingsFile))
        {
            await using FileStream stream = File.OpenRead(_settingsFile);
            settings = Normalize(await JsonSerializer.DeserializeAsync(
                stream,
                SettingsJsonContext.Default.ApplicationSettings,
                cancellationToken));
        }

        return settings;
    }

    public async Task<string> LoadVocabularyAsync(CancellationToken cancellationToken)
    {
        await EnsureVocabularyAsync(cancellationToken);
        return await File.ReadAllTextAsync(
            VocabularyFilePath, new UTF8Encoding(false, true), cancellationToken);
    }

    public async Task EnsureVocabularyAsync(CancellationToken cancellationToken)
    {
        ApplicationSettings settings = await LoadAsync(cancellationToken);
        await EnsureVocabularyFileAsync(settings.TechnicalVocabulary, cancellationToken);
        if (!string.IsNullOrEmpty(settings.TechnicalVocabulary))
        {
            await SaveAsync(settings, cancellationToken);
        }
    }

    public async Task SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await EnsureVocabularyFileAsync(settings.TechnicalVocabulary, cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFile)!);
        string temporaryPath = _settingsFile + ".tmp";
        await using (FileStream stream = new(
            temporaryPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                settings with { TechnicalVocabulary = string.Empty },
                SettingsJsonContext.Default.ApplicationSettings,
                cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Move(temporaryPath, _settingsFile, overwrite: true);
    }

    private async Task EnsureVocabularyFileAsync(string legacyVocabulary, CancellationToken cancellationToken)
    {
        if (File.Exists(VocabularyFilePath)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(VocabularyFilePath)!);
        string temporaryPath = VocabularyFilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, legacyVocabulary, new UTF8Encoding(false), cancellationToken);
            try { File.Move(temporaryPath, VocabularyFilePath); }
            catch (IOException) when (File.Exists(VocabularyFilePath)) { }
        }
        finally { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
    }

    private static ApplicationSettings Normalize(ApplicationSettings? settings)
    {
        ApplicationSettings defaults = new();
        if (settings is null)
        {
            return defaults;
        }

        List<TranscriptionEndpointProfile> endpoints = settings.TranscriptionEndpoints ?? [];
        if (endpoints.Count == 0 && !string.IsNullOrWhiteSpace(settings.AzureEndpoint))
        {
            endpoints = [new TranscriptionEndpointProfile
            {
                Id = TranscriptionEndpointProfile.LegacyId,
                Name = "Existing Azure endpoint",
                Endpoint = settings.AzureEndpoint,
                Deployment = settings.AzureDeployment ?? string.Empty,
                ApiVersion = settings.AzureApiVersion ?? defaults.AzureApiVersion,
            }];
        }

        // System.Text.Json accepts explicit nulls for non-nullable reference properties.
        // Older settings files can therefore bypass the property initializers and pass
        // null strings to application code that correctly relies on this schema.
        return settings with
        {
            TranscriptionEndpoints = endpoints,
            SelectedTranscriptionEndpointId = settings.SelectedTranscriptionEndpointId ?? endpoints.FirstOrDefault()?.Id,
            TranscriptionLanguageMode = TranscriptionLanguageModes.IsValid(settings.TranscriptionLanguageMode)
                ? settings.TranscriptionLanguageMode
                : defaults.TranscriptionLanguageMode,
            LocalWhisperModel = settings.LocalWhisperModel ?? defaults.LocalWhisperModel,
            AzureEndpoint = settings.AzureEndpoint ?? defaults.AzureEndpoint,
            AzureDeployment = settings.AzureDeployment ?? defaults.AzureDeployment,
            AzureApiVersion = settings.AzureApiVersion ?? defaults.AzureApiVersion,
            LmStudioBaseUrl = settings.LmStudioBaseUrl ?? defaults.LmStudioBaseUrl,
            LmStudioModel = settings.LmStudioModel ?? defaults.LmStudioModel,
            OllamaBaseUrl = settings.OllamaBaseUrl ?? defaults.OllamaBaseUrl,
            OllamaModel = settings.OllamaModel ?? defaults.OllamaModel,
            TechnicalVocabulary = settings.TechnicalVocabulary ?? defaults.TechnicalVocabulary,
            TargetWindowPolicy = settings.TargetWindowPolicy ?? defaults.TargetWindowPolicy,
            Hotkey = settings.Hotkey ?? defaults.Hotkey,
            DiagnosticLoggingLevel = settings.DiagnosticLoggingLevel ?? defaults.DiagnosticLoggingLevel,
        };
    }

    [JsonSerializable(typeof(ApplicationSettings))]
    private sealed partial class SettingsJsonContext : JsonSerializerContext;
}
