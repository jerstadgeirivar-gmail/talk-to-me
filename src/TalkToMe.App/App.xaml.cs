using System.IO;
using System.Windows;
using TalkToMe.Core;
using TalkToMe.Infrastructure;

namespace TalkToMe.App;

public partial class App : System.Windows.Application, IDisposable
{
    private SingleInstanceCoordinator? _singleInstance;
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private LocalVoiceCommandService? _voiceCommandService;
    private UpdateCoordinator? _updateCoordinator;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new SingleInstanceCoordinator();
        if (!_singleInstance.IsPrimary)
        {
#if DEBUG
            System.Windows.MessageBox.Show(
                "TalkToMe is already running. Exit the installed version from its system tray icon before starting with F5.",
                "TalkToMe debug startup blocked",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(10);
#else
            Shutdown(0);
#endif
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
        WindowsGlobalHotkeyService hotkeyService = new(settings.Hotkey);
        RecoveredRecording? recoveredRecording =
            options.DiagnosticDataDirectory is null && options.OutputPath is null
                ? PendingRecordingRecovery.FindLatest(TalkToMeDataPaths.PendingAudioDirectory)
                : null;
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
            allowRecordOnly: options.DiagnosticAudioPath is not null && options.DiagnosticTranscript is null,
            recoveredRecording);
        _voiceCommandService = new LocalVoiceCommandService();
        MainWindow window = new(viewModel, hotkeyService, settingsStore, secretStore, _voiceCommandService);
        MainWindow = window;
        window.Show();
        _singleInstance.ActivationRequested += () => Dispatcher.BeginInvoke(() => ShowMainWindow(window));
        InitializeTray(viewModel, window);
        _voiceCommandService.CommandDetected += (_, eventArgs) =>
            Dispatcher.BeginInvoke(() => HandleVoiceCommandAsync(viewModel, eventArgs));
        bool voiceCommandsStarted =
            settings.VoiceCommandsEnabled is not false && _voiceCommandService.Start();
        viewModel.ShowVoiceCommandStatus(voiceCommandsStarted);
#if !DEBUG
        if (options.DiagnosticDataDirectory is null &&
            options.DiagnosticAudioPath is null &&
            IsInstalledApplication())
        {
            _updateCoordinator = new UpdateCoordinator(new GitHubUpdateService(), viewModel);
            _updateCoordinator.Start();
        }
        else
        {
            viewModel.ConfigureUpdates("Portable build", null);
        }
#else
        viewModel.ConfigureUpdates("Development build", null);
#endif
        if (options.StartMinimized)
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
        _voiceCommandService?.Dispose();
        _voiceCommandService = null;
        _updateCoordinator?.Dispose();
        _updateCoordinator = null;
        GC.SuppressFinalize(this);
    }

    private static async Task HandleVoiceCommandAsync(
        MainWindowViewModel viewModel,
        VoiceCommandDetectedEventArgs eventArgs)
    {
        if (!viewModel.CanHandleVoiceCommand)
        {
            return;
        }

        if (viewModel.IsRecording)
        {
            await viewModel.StopFromVoiceCommandAsync(
                eventArgs.TrailingAudio,
                VoiceCommandFeedback.Play);
            return;
        }

        VoiceCommandFeedback.Play();
        await Task.Delay(100);
        await viewModel.StartFromVoiceCommandAsync();
    }

    private static string CreatePendingAudioPath()
    {
        return Path.Combine(
            TalkToMeDataPaths.PendingAudioDirectory,
            $"recording-{DateTime.UtcNow:yyyyMMdd-HHmmss}.wav");
    }

    private static bool IsInstalledApplication()
    {
        string installedDirectory = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "TalkToMe"));
        string processDirectory = Path.GetFullPath(
            Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty);
        return processDirectory.Equals(installedDirectory, StringComparison.OrdinalIgnoreCase);
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
        menu.Items.Add("Show TalkToMe", null, (_, _) => Dispatcher.Invoke(() => ShowMainWindow(window)));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(window.Close));

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)
                ?? System.Drawing.SystemIcons.Application,
            Text = "TalkToMe",
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

