using System.Windows;

namespace TalkToMe.App;

public partial class SettingsWindow : Window, IDisposable
{
    private readonly SettingsWindowViewModel _viewModel;
    private readonly Func<string, bool> _applyHotkey;
    private readonly Func<bool, bool> _applyVoiceCommands;
    private CancellationTokenSource? _operationCancellation;

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
        Closed += (_, _) => Dispose();
    }

    private async void LoadSettings(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadAsync(CancellationToken.None);
    }

    private async void SaveSettings(object sender, RoutedEventArgs e)
    {
        bool saved = await _viewModel.SaveAsync(
            ApiKeyPasswordBox.Password,
            LmStudioTokenPasswordBox.Password,
            OllamaTokenPasswordBox.Password,
            _applyHotkey,
            _applyVoiceCommands,
            CancellationToken.None);
        if (saved)
        {
            ApiKeyPasswordBox.Clear();
            LmStudioTokenPasswordBox.Clear();
            OllamaTokenPasswordBox.Clear();
        }
    }

    private async void RemoveAzureKey(object sender, RoutedEventArgs e)
    {
        await _viewModel.RemoveSecretAsync(TalkToMe.Core.TranscriptionProviderIds.AzureOpenAi, CancellationToken.None);
        ApiKeyPasswordBox.Clear();
    }

    private async void TestProvider(object sender, RoutedEventArgs e)
    {
        _operationCancellation?.Cancel();
        _operationCancellation = new CancellationTokenSource();
        await _viewModel.TestProviderAsync(_operationCancellation.Token);
    }

    private async void RepairModel(object sender, RoutedEventArgs e)
    {
        _operationCancellation?.Cancel();
        _operationCancellation = new CancellationTokenSource();
        await _viewModel.RepairModelAsync(_operationCancellation.Token);
    }

    private void CancelOperation(object sender, RoutedEventArgs e) => _operationCancellation?.Cancel();

    private void CloseSettings(object sender, RoutedEventArgs e) { _operationCancellation?.Cancel(); Close(); }

    public void Dispose()
    {
        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();
        _operationCancellation = null;
        GC.SuppressFinalize(this);
    }
}
