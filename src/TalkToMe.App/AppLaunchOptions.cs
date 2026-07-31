using System.Globalization;
using System.IO;

namespace TalkToMe.App;

internal sealed record AppLaunchOptions(
    string? DiagnosticAudioPath,
    string? OutputPath,
    string? DiagnosticTranscript,
    string? ImportKeyFile,
    string? DiagnosticDataDirectory,
    double DiagnosticSpeed,
    bool UseConfiguredProvider,
    bool ShowWindow,
    bool StartMinimized)
{
    public static AppLaunchOptions Parse(string[] arguments)
    {
        string? diagnosticAudioPath = null;
        string? outputPath = null;
        string? diagnosticTranscript = null;
        string? importKeyFile = null;
        string? diagnosticDataDirectory = null;
        double diagnosticSpeed = 1;
        bool useConfiguredProvider = false;
        bool showWindow = false;
        bool startMinimized = false;

        for (int index = 0; index < arguments.Length; index++)
        {
            string option = arguments[index];
            if (option == "--show-window")
            {
                showWindow = true;
                continue;
            }

            if (option == "--minimized")
            {
                startMinimized = true;
                continue;
            }

            if (option == "--diagnostic-live-provider")
            {
                useConfiguredProvider = true;
                continue;
            }

            string value = RequireValue(arguments, ref index);
            switch (option)
            {
                case "--diagnostic-audio":
                    diagnosticAudioPath = Path.GetFullPath(value);
                    break;
                case "--diagnostic-output":
                    outputPath = Path.GetFullPath(value);
                    break;
                case "--diagnostic-speed":
                    diagnosticSpeed = double.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "--diagnostic-transcript":
                    diagnosticTranscript = value;
                    break;
                case "--import-key-file":
                    importKeyFile = Path.GetFullPath(value);
                    break;
                case "--diagnostic-data-directory":
                    diagnosticDataDirectory = Path.GetFullPath(value);
                    break;
                default:
                    throw new ArgumentException($"Unknown application argument '{option}'.");
            }
        }

        if (diagnosticAudioPath is not null && !File.Exists(diagnosticAudioPath))
        {
            throw new FileNotFoundException("The diagnostic audio file does not exist.", diagnosticAudioPath);
        }

        if (diagnosticAudioPath is not null && outputPath is null)
        {
            throw new ArgumentException("--diagnostic-output is required with --diagnostic-audio.");
        }

        if (diagnosticSpeed <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(arguments), "Diagnostic speed must be positive.");
        }

        return new AppLaunchOptions(
            diagnosticAudioPath,
            outputPath,
            diagnosticTranscript,
            importKeyFile,
            diagnosticDataDirectory,
            diagnosticSpeed,
            useConfiguredProvider,
            showWindow,
            startMinimized);
    }

    private static string RequireValue(string[] arguments, ref int index)
    {
        if (index + 1 >= arguments.Length)
        {
            throw new ArgumentException($"Application argument '{arguments[index]}' requires a value.");
        }

        index++;
        return arguments[index];
    }
}
