using System.Text.Json;
using System.Text.Json.Serialization;
using TalkToMe.Core;

namespace TalkToMe.Infrastructure;

public sealed partial class JsonApplicationSettingsStore(string? settingsFile = null) : IApplicationSettingsStore
{
    private readonly string _settingsFile = settingsFile ?? TalkToMeDataPaths.SettingsFile;

    public async Task<ApplicationSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_settingsFile))
        {
            return new ApplicationSettings();
        }

        await using FileStream stream = File.OpenRead(_settingsFile);
        ApplicationSettings? settings = await JsonSerializer.DeserializeAsync(
            stream,
            SettingsJsonContext.Default.ApplicationSettings,
            cancellationToken);
        return Normalize(settings);
    }

    public async Task SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
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
                settings,
                SettingsJsonContext.Default.ApplicationSettings,
                cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Move(temporaryPath, _settingsFile, overwrite: true);
    }

    private static ApplicationSettings Normalize(ApplicationSettings? settings)
    {
        ApplicationSettings defaults = new();
        if (settings is null)
        {
            return defaults;
        }

        // System.Text.Json accepts explicit nulls for non-nullable reference properties.
        // Older settings files can therefore bypass the property initializers and pass
        // null strings to application code that correctly relies on this schema.
        return settings with
        {
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
