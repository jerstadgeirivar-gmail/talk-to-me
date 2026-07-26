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
        return settings ?? new ApplicationSettings();
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

    [JsonSerializable(typeof(ApplicationSettings))]
    private sealed partial class SettingsJsonContext : JsonSerializerContext;
}
