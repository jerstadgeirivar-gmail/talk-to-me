using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using TalkToMe.Core;
using TalkToMe.Infrastructure;

namespace TalkToMe.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly IApplicationSettingsStore _settingsStore;
    private readonly INamedSecretStore _secretStore;
    private readonly ITranscriptionProviderFactory _providerFactory;
    private readonly TranscriptionProviderCoordinator? _providerCoordinator;
    private readonly LocalVoiceCommandService _voiceCommandService;
    private HwndSource? _windowSource;
    private bool _closeRequested;
    private bool _canClose;

    public MainWindow(
        MainWindowViewModel viewModel,
        IGlobalHotkeyService hotkeyService,
        IApplicationSettingsStore settingsStore,
        INamedSecretStore secretStore,
        ITranscriptionProviderFactory providerFactory,
        TranscriptionProviderCoordinator? providerCoordinator,
        LocalVoiceCommandService voiceCommandService)
    {

        _viewModel = viewModel;
        _hotkeyService = hotkeyService;
        _settingsStore = settingsStore;
        _secretStore = secretStore;
        _providerFactory = providerFactory;
        _providerCoordinator = providerCoordinator;
        _voiceCommandService = voiceCommandService;
        InitializeComponent();
        DataContext = viewModel;
    }

    public void ShowSettingsWindow()
    {
        SettingsWindow settingsWindow = new(
            new SettingsWindowViewModel(
                _settingsStore,
                _secretStore,
                _providerFactory,
                (providerId, cancellationToken) => _providerCoordinator?.ApplySelectionAsync(providerId, cancellationToken) ?? Task.CompletedTask),
            ApplyHotkey,
            ApplyVoiceCommands)
        {
            Owner = this,
        };
        settingsWindow.ShowDialog();
    }

    private bool ApplyVoiceCommands(bool enabled)
    {
        if (!enabled)
        {
            _voiceCommandService.Stop();
            _viewModel.ShowVoiceCommandStatus(false);
            return true;
        }

        bool started = _voiceCommandService.Start();
        _viewModel.ShowVoiceCommandStatus(started);
        return started;
    }

    private bool ApplyHotkey(string hotkey)
    {
        bool registered = _hotkeyService.Reconfigure(hotkey);
        if (!registered)
        {
            _viewModel.ShowHotkeyRegistrationFailure(hotkey);
        }

        return registered;
    }

    private void OpenSettings(object sender, RoutedEventArgs e) => ShowSettingsWindow();

    private void HideToTray(object sender, RoutedEventArgs e) => Hide();

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        nint windowHandle = new WindowInteropHelper(this).Handle;
        _windowSource = HwndSource.FromHwnd(windowHandle);
        _windowSource.AddHook(ProcessWindowMessage);
        if (!_hotkeyService.Register(windowHandle))
        {
            _viewModel.ShowHotkeyRegistrationFailure(_hotkeyService.GestureText);
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_canClose)
        {
            e.Cancel = true;
            if (!_closeRequested)
            {
                _closeRequested = true;
                _windowSource?.RemoveHook(ProcessWindowMessage);
                _hotkeyService.Dispose();
                _ = FinishClosingAsync();
            }
        }

        base.OnClosing(e);
    }

    private async Task FinishClosingAsync()
    {
        await _viewModel.DisposeAsync();
        _canClose = true;
        await Dispatcher.BeginInvoke(Close);
    }

    private nint ProcessWindowMessage(
        nint windowHandle,
        int message,
        nint wordParameter,
        nint longParameter,
        ref bool handled)
    {
        if (message == _hotkeyService.WindowMessage && wordParameter == _hotkeyService.HotkeyId)
        {
            _ = _viewModel.HandleHotkeyAsync(
                VoiceCommandFeedback.PlayStart,
                VoiceCommandFeedback.PlayStop);
            handled = true;
        }

        return 0;
    }
}
