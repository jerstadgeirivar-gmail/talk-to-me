namespace TalkToMe.Core;

public sealed record ApplicationSettings
{
    public string? TranscriptionProviderId { get; init; }

    public string TranscriptionLanguageMode { get; init; } = TranscriptionLanguageModes.Norwegian;

    public string LocalWhisperModel { get; init; } = "small-q5_1";

    public string AzureEndpoint { get; init; } = string.Empty;

    public string AzureDeployment { get; init; } = string.Empty;

    public string AzureApiVersion { get; init; } = "2025-04-01-preview";

    public string LmStudioBaseUrl { get; init; } = "http://localhost:1234";

    public string LmStudioModel { get; init; } = string.Empty;

    public string OllamaBaseUrl { get; init; } = "http://localhost:11434";

    public string OllamaModel { get; init; } = string.Empty;

    public string TechnicalVocabulary { get; init; } = string.Empty;

    public bool RetainFailedAudio { get; init; }

    public bool StartWithWindows { get; init; }

    public bool ClipboardOnlyMode { get; init; }

    public string TargetWindowPolicy { get; init; } = "OriginalTarget";

    public string Hotkey { get; init; } = "Win+<";

    public bool? VoiceCommandsEnabled { get; init; }

    public string DiagnosticLoggingLevel { get; init; } = "Information";
}

public static class TranscriptionLanguageModes
{
    public const string Auto = "auto";
    public const string Norwegian = "norwegian";
    public const string NorwegianEnglish = "norwegian-english";

    public static bool IsValid(string? value) => value is Auto or Norwegian or NorwegianEnglish;
}
