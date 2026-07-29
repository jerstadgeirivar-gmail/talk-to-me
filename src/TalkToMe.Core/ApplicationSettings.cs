namespace TalkToMe.Core;

public sealed record ApplicationSettings
{
    public string AzureEndpoint { get; init; } = string.Empty;

    public string AzureDeployment { get; init; } = string.Empty;

    public string AzureApiVersion { get; init; } = "2025-04-01-preview";

    public string TechnicalVocabulary { get; init; } = string.Empty;

    public bool RetainFailedAudio { get; init; } = true;

    public bool StartWithWindows { get; init; }

    public bool ClipboardOnlyMode { get; init; }

    public string TargetWindowPolicy { get; init; } = "OriginalTarget";

    public string Hotkey { get; init; } = "Win+<";

    public bool? VoiceCommandsEnabled { get; init; }

    public string DiagnosticLoggingLevel { get; init; } = "Information";
}
