using System.IO;
using System.Windows;
using VoiceType.Core;
using VoiceType.Infrastructure;

namespace VoiceType.App;

public partial class App : System.Windows.Application, IDisposable
{
    private SingleInstanceCoordinator? _singleInstance;
    private System.Windows.Forms.NotifyIcon? _trayIcon;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new SingleInstanceCoordinator();
        if (!_singleInstance.IsPrimary)
        {
            Shutdown(0);
            return;
        }

        AppLaunchOptions options = AppLaunchOptions.Parse(e.Args);
        string? diagnosticDataDirectory = options.DiagnosticDataDirectory;
        JsonApplicationSettingsStore settingsStore = new(
            diagnosticDataDirectory is null
                ? null
                : Path.Combine(diagnosticDataDirectory, "settings.json"));
        DpapiSecretStore secretStore = new(
            diagnosticDataDirectory is null
                ? null
                : Path.Combine(diagnosticDataDirectory, "credential.bin"));
        if (options.ImportKeyFile is not null)
        {
            string importedKey = await File.ReadAllTextAsync(options.ImportKeyFile);
            await secretStore.SetSecretAsync(importedKey.Trim(), CancellationToken.None);
            Shutdown(0);
            return;
        }

        ApplicationSettings settings = await settingsStore.LoadAsync(CancellationToken.None);
        string? storedApiKey = await secretStore.GetSecretAsync(CancellationToken.None);
        IAudioSource audioSource = options.DiagnosticAudioPath is null
            ? new WasapiMicrophoneAudioSource()
            : new FileBackedAudioSource(options.DiagnosticAudioPath, options.DiagnosticSpeed);
        AzureTranscriptionOptions? azureOptions =
            AzureTranscriptionOptions.FromConfiguration(settings, storedApiKey);
        ITranscriptionProvider? transcriptionProvider = options.DiagnosticTranscript is not null
            ? new DiagnosticTranscriptionProvider(options.DiagnosticTranscript)
            : azureOptions is null
                ? null
                : new AzureTranscriptionProvider(azureOptions);
        WindowsWindowTargetService windowTargetService = new(Environment.ProcessId);
        WindowsGlobalHotkeyService hotkeyService = new();
        string outputPath = options.OutputPath ?? CreatePendingAudioPath();
        MainWindowViewModel viewModel = new(
            audioSource,
            new WaveAudioRecordingService(),
            new ApplicationStateController(),
            transcriptionProvider,
            windowTargetService,
            new ClipboardTextInsertionService(
                windowTargetService,
                new WpfClipboardAdapter(),
                new WindowsKeyboardInputAdapter()),
            hotkeyService,
            outputPath,
            allowRecordOnly: options.DiagnosticAudioPath is not null && options.DiagnosticTranscript is null);
        MainWindow window = new(viewModel, hotkeyService, settingsStore, secretStore);
        MainWindow = window;
        window.Show();
        _singleInstance.ActivationRequested += () => Dispatcher.BeginInvoke(() => ShowMainWindow(window));
        InitializeTray(viewModel, window);
        if (options.DiagnosticAudioPath is null && azureOptions is not null && !options.ShowWindow)
        {
            window.Hide();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Dispose();
        base.OnExit(e);
    }

    public void Dispose()
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
        _singleInstance?.Dispose();
        _singleInstance = null;
        GC.SuppressFinalize(this);
    }

    private static string CreatePendingAudioPath()
    {
        return Path.Combine(
            VoiceTypeDataPaths.PendingAudioDirectory,
            $"recording-{DateTime.UtcNow:yyyyMMdd-HHmmss}.wav");
    }

    private void InitializeTray(MainWindowViewModel viewModel, MainWindow window)
    {
        System.Windows.Forms.ContextMenuStrip menu = new();
        menu.Items.Add(
            "Start/stop dictation",
            null,
            (_, _) => viewModel.ToggleRecording(insertAfterTranscription: true));
        menu.Items.Add("Cancel recording", null, (_, _) => viewModel.CancelCommand.Execute(null));
        menu.Items.Add("Retry pending transcription", null, (_, _) => viewModel.RetryCommand.Execute(null));
        menu.Items.Add("Delete pending audio", null, (_, _) => viewModel.DeletePendingCommand.Execute(null));
        menu.Items.Add("Copy last transcript", null, (_, _) => viewModel.CopyTranscriptToClipboard());
        menu.Items.Add("Settings", null, (_, _) => Dispatcher.Invoke(window.ShowSettingsWindow));
        menu.Items.Add("Show VoiceType", null, (_, _) => Dispatcher.Invoke(() => ShowMainWindow(window)));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(window.Close));

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = System.Drawing.SystemIcons.Application,
            Text = "VoiceType",
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(() => ShowMainWindow(window));
    }

    private static void ShowMainWindow(MainWindow window)
    {
        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
    }
}

