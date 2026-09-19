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
    private readonly object _activeCheckGate = new();
    private readonly Dispatcher _dispatcher;
    private Task? _activeCheck;
    private volatile bool _disposed;

    public UpdateCoordinator(
        GitHubUpdateService updateService,
        MainWindowViewModel viewModel)
    {
        _updateService = updateService;
        _viewModel = viewModel;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _timer = new DispatcherTimer
        {
            Interval = CheckInterval,
        };
        _timer.Tick += OnTimerTick;
        _viewModel.ConfigureUpdates(
            $"Version {_updateService.CurrentVersion}",
            () => StartCheckAsync(userInitiated: true));
    }

    public void Start()
    {
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        _timer.Start();
        _ = StartCheckAsync(userInitiated: false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Task? activeCheck;
        lock (_activeCheckGate)
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
            activeCheck = _activeCheck;
        }

        try
        {
            activeCheck?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            _checkLock.Dispose();
            _lifetimeCancellation.Dispose();
        }
    }

    private Task StartCheckAsync(bool userInitiated)
    {
        lock (_activeCheckGate)
        {
            if (_disposed || (_activeCheck is not null && !_activeCheck.IsCompleted))
            {
                return Task.CompletedTask;
            }

            _activeCheck = CheckAsync(userInitiated);
            return _activeCheck;
        }
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
                SetUpdateStatus("Checking for updates…");
            }

            UpdateAvailability update =
                await _updateService.CheckAsync(_lifetimeCancellation.Token)
                    .ConfigureAwait(false);
            if (!update.IsAvailable)
            {
                SetUpdateStatus(update.Status);
                return;
            }

            if (!_viewModel.CanInstallUpdate)
            {
                SetUpdateStatus(
                    $"Version {update.Version} available; waiting until dictation is idle");
                return;
            }

            SetUpdateStatus($"Installing version {update.Version}…");
            string installerPath = await GitHubUpdateService.DownloadAndVerifyAsync(
                update,
                _lifetimeCancellation.Token).ConfigureAwait(false);
            _lifetimeCancellation.Token.ThrowIfCancellationRequested();
            DispatcherOperation launchOperation = _dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                new Action(() =>
                {
                    if (_disposed || _lifetimeCancellation.IsCancellationRequested)
                    {
                        return;
                    }

                    GitHubUpdateService.StartInstaller(installerPath);
                    System.Windows.Application.Current.Shutdown();
                }));
            _ = launchOperation.Task.ContinueWith(
                completedOperation =>
                {
                    _ = completedOperation.Exception;
                    SetUpdateStatus("Update failed; will retry later");
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            SetUpdateStatus("Update failed; will retry later");
        }
        finally
        {
            _checkLock.Release();
        }
    }

    private void OnTimerTick(object? sender, EventArgs eventArgs) =>
        _ = StartCheckAsync(userInitiated: false);

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs eventArgs)
    {
        if (eventArgs.Mode == PowerModes.Resume)
        {
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(
                () => StartCheckAsync(userInitiated: false));
        }
    }

    private void SetUpdateStatus(string status)
    {
        if (_disposed)
        {
            return;
        }

        if (_dispatcher.CheckAccess())
        {
            _viewModel.SetUpdateStatus(status);
            return;
        }

        _ = _dispatcher.BeginInvoke(
            DispatcherPriority.Normal,
            new Action(() =>
            {
                if (!_disposed)
                {
                    _viewModel.SetUpdateStatus(status);
                }
            }));
    }
}
