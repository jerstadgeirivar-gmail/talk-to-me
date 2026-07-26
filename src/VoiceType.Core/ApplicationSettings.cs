namespace VoiceType.Core;

public sealed record ApplicationSettings
{
    public string AzureEndpoint { get; init; } = string.Empty;

    public string AzureDeployment { get; init; } = string.Empty;

    public string AzureApiVersion { get; init; } = "preview";

    public string TechnicalVocabulary { get; init; } = string.Empty;

    public bool RetainFailedAudio { get; init; } = true;

    public bool StartWithWindows { get; init; }

    public bool ClipboardOnlyMode { get; init; }

    public string TargetWindowPolicy { get; init; } = "OriginalTarget";

    public string Hotkey { get; init; } = "Ctrl+Alt+F9";

    public string DiagnosticLoggingLevel { get; init; } = "Information";
}
