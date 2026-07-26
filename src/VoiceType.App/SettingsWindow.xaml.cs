using System.Windows;

namespace VoiceType.App;

public partial class SettingsWindow : Window
{
    private readonly SettingsWindowViewModel _viewModel;

    public SettingsWindow(SettingsWindowViewModel viewModel)
    {
        _viewModel = viewModel;
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
        bool saved = await _viewModel.SaveAsync(ApiKeyPasswordBox.Password, CancellationToken.None);
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
