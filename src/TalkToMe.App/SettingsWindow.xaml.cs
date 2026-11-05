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
        if (HasValidationErrors(this)) { _viewModel.ShowValidationError("Correct the invalid numeric limits before saving."); return; }
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
        await _viewModel.RemoveSecretAsync(_viewModel.ActiveEndpointCredentialId, CancellationToken.None);
        ApiKeyPasswordBox.Clear();
    }

    private async void TestProvider(object sender, RoutedEventArgs e)
    {
        if (HasValidationErrors(this)) { _viewModel.ShowValidationError("Correct the invalid numeric limits before testing."); return; }
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

    private void AddEndpoint(object sender, RoutedEventArgs e) { ApiKeyPasswordBox.Clear(); _viewModel.AddEndpoint(false); }
    private void DuplicateEndpoint(object sender, RoutedEventArgs e) { ApiKeyPasswordBox.Clear(); _viewModel.AddEndpoint(true); }
    private void RemoveEndpoint(object sender, RoutedEventArgs e) { ApiKeyPasswordBox.Clear(); _viewModel.RemoveEndpoint(); }
    private void SelectEndpoint(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => ApiKeyPasswordBox.Clear();
    private async void ClearProgress(object sender, RoutedEventArgs e) => await _viewModel.ClearProgressAsync();

    private static bool HasValidationErrors(DependencyObject element)
    {
        if (System.Windows.Controls.Validation.GetHasError(element)) return true;
        for (int index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(element); index++)
            if (HasValidationErrors(System.Windows.Media.VisualTreeHelper.GetChild(element, index))) return true;
        return false;
    }

    private async void EditVocabulary(object sender, RoutedEventArgs e) =>
        await _viewModel.EditVocabularyAsync(CancellationToken.None);

    private void CloseSettings(object sender, RoutedEventArgs e) { _operationCancellation?.Cancel(); Close(); }

    public void Dispose()
    {
        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();
        _operationCancellation = null;
        GC.SuppressFinalize(this);
    }
}
