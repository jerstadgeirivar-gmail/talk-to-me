using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using TalkToMe.Core;
using TalkToMe.Infrastructure;

namespace TalkToMe.App;

public partial class LocalModelSetupWindow : Window, INotifyPropertyChanged, IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private double _progressValue;
    private string _statusText = "Connecting to the model source…";
    private bool _completed;

    public LocalModelSetupWindow()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += StartDownload;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public double ProgressValue
    {
        get => _progressValue;
        private set { _progressValue = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(); }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_completed) _cancellation.Cancel();
        base.OnClosing(e);
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        _cancellation.Dispose();
        GC.SuppressFinalize(this);
    }

    private async void StartDownload(object sender, RoutedEventArgs e)
    {
        Loaded -= StartDownload;
        try
        {
            Progress<double> progress = new(value =>
            {
                ProgressValue = value;
                StatusText = value < 100 ? $"Downloading… {value:N0}%" : "Verifying model integrity…";
            });
            await LocalWhisperModel.RepairAsync(progress, _cancellation.Token);
            ProviderTestResult result = await LocalWhisperModel.VerifyAsync(_cancellation.Token);
            if (!result.IsReady) throw new InvalidDataException(result.Message);
            _completed = true;
            StatusText = "Local Whisper is ready.";
            DialogResult = true;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Download cancelled. You can retry from Settings.";
            DialogResult = false;
        }
        catch (Exception exception)
        {
            StatusText = $"Setup failed: {exception.Message}";
            System.Windows.MessageBox.Show(this, StatusText, "Local Whisper setup failed", MessageBoxButton.OK, MessageBoxImage.Error);
            DialogResult = false;
        }
    }

    private void CancelDownload(object sender, RoutedEventArgs e) => _cancellation.Cancel();

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
