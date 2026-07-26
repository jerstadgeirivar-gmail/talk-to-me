namespace TalkToMe.App;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = "Local\\TalkToMe.Application.Mutex";
    private const string ActivationEventName = "Local\\TalkToMe.Application.Activate";
    private readonly Mutex _mutex;
    private readonly EventWaitHandle? _activationEvent;
    private readonly CancellationTokenSource? _cancellation;

    public SingleInstanceCoordinator()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out bool isPrimary);
        IsPrimary = isPrimary;
        if (!isPrimary)
        {
            using EventWaitHandle activationEvent = EventWaitHandle.OpenExisting(ActivationEventName);
            activationEvent.Set();
            return;
        }

        _activationEvent = new EventWaitHandle(
            initialState: false,
            EventResetMode.AutoReset,
            ActivationEventName);
        _cancellation = new CancellationTokenSource();
        _ = Task.Run(() => WaitForActivation(_cancellation.Token));
    }

    public event Action? ActivationRequested;

    public bool IsPrimary { get; }

    public void Dispose()
    {
        _cancellation?.Cancel();
        _activationEvent?.Set();
        _activationEvent?.Dispose();
        _cancellation?.Dispose();
        if (IsPrimary)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
    }

    private void WaitForActivation(CancellationToken cancellationToken)
    {
        WaitHandle[] handles = [_activationEvent!, cancellationToken.WaitHandle];
        while (!cancellationToken.IsCancellationRequested)
        {
            int signaled = WaitHandle.WaitAny(handles);
            if (signaled == 0 && !cancellationToken.IsCancellationRequested)
            {
                ActivationRequested?.Invoke();
            }
        }
    }
}
