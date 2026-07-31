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
        IAudioSource audioSource = options.DiagnosticAudioPath is null
            ? new WasapiMicrophoneAudioSource()
            : new FileBackedAudioSource(options.DiagnosticAudioPath, options.DiagnosticSpeed);
        TranscriptionProviderRegistry providerRegistry = new(settingsStore, secretStore);
        bool hasAzureSecret = await secretStore.HasSecretAsync(TranscriptionProviderIds.AzureOpenAi, CancellationToken.None);
        string selectedProviderId = providerRegistry.SelectProviderId(settings, hasAzureSecret);
        if (settings.TranscriptionProviderId is null)
        {
            settings = settings with { TranscriptionProviderId = selectedProviderId };
            await settingsStore.SaveAsync(settings, CancellationToken.None);
        }
        TranscriptionProviderCoordinator? providerCoordinator = options.DiagnosticTranscript is null
            ? new(providerRegistry, settingsStore, selectedProviderId)
            : null;
        ITranscriptionProvider transcriptionProvider = options.DiagnosticTranscript is not null
            ? new DiagnosticTranscriptionProvider(options.DiagnosticTranscript)
            : providerCoordinator!;
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
        MainWindow window = new(viewModel, hotkeyService, settingsStore, secretStore, providerRegistry, providerCoordinator, _voiceCommandService);
        MainWindow = window;
        window.Show();
        if (selectedProviderId == TranscriptionProviderIds.LocalWhisper &&
            options.DiagnosticTranscript is null)
        {
            await EnsureLocalModelAsync(window, viewModel, providerCoordinator!);
        }
        _singleInstance.ActivationRequested += () => Dispatcher.BeginInvoke(() => ShowMainWindow(window));
        InitializeTray(viewModel, window);
        _voiceCommandService.CommandDetected += (_, eventArgs) =>
            Dispatcher.BeginInvoke(() => HandleVoiceCommandAsync(viewModel, eventArgs));
        bool voiceCommandsStarted =
            settings.VoiceCommandsEnabled is true && _voiceCommandService.Start();
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
                VoiceCommandFeedback.PlayStop);
            return;
        }

        VoiceCommandFeedback.PlayStart();
        await Task.Delay(100);
        await viewModel.StartFromVoiceCommandAsync();
    }

    private static string CreatePendingAudioPath()
    {
        return Path.Combine(
            TalkToMeDataPaths.PendingAudioDirectory,
            $"recording-{DateTime.UtcNow:yyyyMMdd-HHmmss}.wav");
    }

    private static async Task EnsureLocalModelAsync(
        MainWindow owner,
        MainWindowViewModel viewModel,
        TranscriptionProviderCoordinator coordinator)
    {
        ProviderTestResult readiness = await LocalWhisperModel.VerifyAsync(CancellationToken.None);
        if (readiness.IsReady)
        {
            return;
        }

        string? customModelPath = Environment.GetEnvironmentVariable("TALKTOME_WHISPER_MODEL_PATH");
        if (!string.IsNullOrWhiteSpace(customModelPath))
        {
            viewModel.ShowProviderStatus(readiness.Message);
            return;
        }

        MessageBoxResult choice = System.Windows.MessageBox.Show(
            owner,
            "TalkToMe needs the default multilingual Local Whisper model for offline transcription." +
            Environment.NewLine + Environment.NewLine +
            "Download and set it up now? The download is about 181 MB. Audio will stay on this computer during transcription.",
            "Set up offline transcription",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);
        if (choice != MessageBoxResult.Yes)
        {
            viewModel.ShowProviderStatus("Local Whisper is not ready. Open Settings to download the model.");
            return;
        }

        using LocalModelSetupWindow setupWindow = new() { Owner = owner };
        bool installed = setupWindow.ShowDialog() is true;
        if (installed)
        {
            await coordinator.ApplySelectionAsync(TranscriptionProviderIds.LocalWhisper, CancellationToken.None);
            viewModel.ShowProviderStatus("Ready — Local Whisper is installed for offline transcription");
        }
        else
        {
            viewModel.ShowProviderStatus("Local Whisper setup was not completed. Open Settings to retry.");
        }
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

