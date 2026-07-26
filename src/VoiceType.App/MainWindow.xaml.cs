using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using VoiceType.Core;

namespace VoiceType.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly IApplicationSettingsStore _settingsStore;
    private readonly ISecretStore _secretStore;
    private HwndSource? _windowSource;
    private bool _isClosing;

    public MainWindow(
        MainWindowViewModel viewModel,
        IGlobalHotkeyService hotkeyService,
        IApplicationSettingsStore settingsStore,
        ISecretStore secretStore)
    {
        _viewModel = viewModel;
        _hotkeyService = hotkeyService;
        _settingsStore = settingsStore;
        _secretStore = secretStore;
        InitializeComponent();
        DataContext = viewModel;
    }

    public void ShowSettingsWindow()
    {
        SettingsWindow settingsWindow = new(
            new SettingsWindowViewModel(_settingsStore, _secretStore))
        {
            Owner = this,
        };
        settingsWindow.ShowDialog();
    }

    private void OpenSettings(object sender, RoutedEventArgs e) => ShowSettingsWindow();

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        nint windowHandle = new WindowInteropHelper(this).Handle;
        _windowSource = HwndSource.FromHwnd(windowHandle);
        _windowSource.AddHook(ProcessWindowMessage);
        if (!_hotkeyService.Register(windowHandle))
        {
            _viewModel.ShowHotkeyRegistrationFailure();
        }
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        if (!_isClosing)
        {
            e.Cancel = true;
            _windowSource?.RemoveHook(ProcessWindowMessage);
            _hotkeyService.Dispose();
            await _viewModel.DisposeAsync();
            _isClosing = true;
            Close();
        }

        base.OnClosing(e);
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
            _viewModel.ToggleRecording();
            handled = true;
        }

        return 0;
    }
}
