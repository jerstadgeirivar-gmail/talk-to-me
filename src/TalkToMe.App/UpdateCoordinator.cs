using System.Windows.Threading;
using Microsoft.Win32;

namespace TalkToMe.App;

internal sealed class UpdateCoordinator : IDisposable
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);
    private readonly GitHubUpdateService _updateService;
    private readonly MainWindowViewModel _viewModel;
    private readonly DispatcherTimer _timer;
    private readonly SemaphoreSlim _checkLock = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private bool _disposed;

    public UpdateCoordinator(
        GitHubUpdateService updateService,
        MainWindowViewModel viewModel)
    {
        _updateService = updateService;
        _viewModel = viewModel;
        _timer = new DispatcherTimer
        {
            Interval = CheckInterval,
        };
        _timer.Tick += OnTimerTick;
        _viewModel.ConfigureUpdates(
            $"Version {_updateService.CurrentVersion}",
            () => CheckAsync(userInitiated: true));
    }

    public void Start()
    {
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        _timer.Start();
        _ = CheckAsync(userInitiated: false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _lifetimeCancellation.Cancel();
    }

    private async Task CheckAsync(bool userInitiated)
    {
        if (!await _checkLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            if (userInitiated)
            {
                _viewModel.SetUpdateStatus("Checking for updates…");
            }

            UpdateAvailability update =
                await _updateService.CheckAsync(_lifetimeCancellation.Token);
            if (!update.IsAvailable)
            {
                _viewModel.SetUpdateStatus(update.Status);
                return;
            }

            if (!_viewModel.CanInstallUpdate)
            {
                _viewModel.SetUpdateStatus(
                    $"Version {update.Version} available; waiting until dictation is idle");
                return;
            }

            _viewModel.SetUpdateStatus($"Installing version {update.Version}…");
            string installerPath = await GitHubUpdateService.DownloadAndVerifyAsync(
                update,
                _lifetimeCancellation.Token);
            GitHubUpdateService.StartInstaller(installerPath);
            System.Windows.Application.Current.Shutdown();
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            _viewModel.SetUpdateStatus("Update failed; will retry later");
        }
        finally
        {
            _checkLock.Release();
        }
    }

    private void OnTimerTick(object? sender, EventArgs eventArgs) =>
        _ = CheckAsync(userInitiated: false);

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs eventArgs)
    {
        if (eventArgs.Mode == PowerModes.Resume)
        {
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(
                () => CheckAsync(userInitiated: false));
        }
    }
}
