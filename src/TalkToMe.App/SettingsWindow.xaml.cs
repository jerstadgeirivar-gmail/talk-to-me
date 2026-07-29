using System.Windows;

namespace TalkToMe.App;

public partial class SettingsWindow : Window
{
    private readonly SettingsWindowViewModel _viewModel;
    private readonly Func<string, bool> _applyHotkey;
    private readonly Func<bool, bool> _applyVoiceCommands;

    public SettingsWindow(
        SettingsWindowViewModel viewModel,
        Func<string, bool> applyHotkey,
        Func<bool, bool> applyVoiceCommands)
    {
        _viewModel = viewModel;
        _applyHotkey = applyHotkey;
        _applyVoiceCommands = applyVoiceCommands;
        InitializeComponent();
        DataContext = viewModel;
        Loaded += LoadSettings;
    }

    private async void LoadSettings(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadAsync(CancellationToken.None);
    }

    private async void SaveSettings(object sender, RoutedEventArgs e)
    {
        bool saved = await _viewModel.SaveAsync(
            ApiKeyPasswordBox.Password,
            _applyHotkey,
            _applyVoiceCommands,
            CancellationToken.None);
        if (saved)
        {
            ApiKeyPasswordBox.Clear();
        }
    }

    private async void RemoveApiKey(object sender, RoutedEventArgs e)
    {
        await _viewModel.RemoveKeyAsync(CancellationToken.None);
        ApiKeyPasswordBox.Clear();
    }

    private void CloseSettings(object sender, RoutedEventArgs e) => Close();
}
