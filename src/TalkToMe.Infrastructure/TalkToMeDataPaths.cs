namespace TalkToMe.Infrastructure;

public static class TalkToMeDataPaths
{
    public static string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TalkToMe");

    public static string PendingAudioDirectory { get; } = Path.Combine(RootDirectory, "Pending");

    public static string DiagnosticsDirectory { get; } = Path.Combine(RootDirectory, "Diagnostics");

    public static string SettingsFile { get; } = Path.Combine(RootDirectory, "settings.json");

    public static string ProtectedSecretFile { get; } = Path.Combine(RootDirectory, "credential.bin");
}
