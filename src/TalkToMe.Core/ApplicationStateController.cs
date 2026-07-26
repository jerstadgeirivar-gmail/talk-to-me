namespace TalkToMe.Core;

public sealed class ApplicationStateController : IApplicationStateController
{
    private static readonly Dictionary<DictationState, HashSet<DictationState>> AllowedTransitions =
        new()
        {
            [DictationState.Idle] = Set(DictationState.StartingRecording, DictationState.RecoverableFailure),
            [DictationState.StartingRecording] = Set(
                DictationState.Recording,
                DictationState.Cancelling,
                DictationState.RecoverableFailure,
                DictationState.FatalFailure),
            [DictationState.Recording] = Set(
                DictationState.StoppingRecording,
                DictationState.Cancelling,
                DictationState.RecoverableFailure),
            [DictationState.StoppingRecording] = Set(
                DictationState.PreparingAudio,
                DictationState.RecoverableFailure),
            [DictationState.PreparingAudio] = Set(
                DictationState.Transcribing,
                DictationState.Completed,
                DictationState.RecoverableFailure),
            [DictationState.Transcribing] = Set(
                DictationState.ReadyToInsert,
                DictationState.Cancelling,
                DictationState.RecoverableFailure,
                DictationState.FatalFailure),
            [DictationState.ReadyToInsert] = Set(
                DictationState.Inserting,
                DictationState.Completed,
                DictationState.RecoverableFailure),
            [DictationState.Inserting] = Set(
                DictationState.Completed,
                DictationState.RecoverableFailure),
            [DictationState.Completed] = Set(DictationState.Idle),
            [DictationState.Cancelling] = Set(DictationState.Cancelled, DictationState.RecoverableFailure),
            [DictationState.Cancelled] = Set(DictationState.Idle),
            [DictationState.RecoverableFailure] = Set(DictationState.Idle, DictationState.Transcribing),
            [DictationState.FatalFailure] = Set(DictationState.Idle),
        };

    private readonly object _syncRoot = new();
    private DictationState _current = DictationState.Idle;

    public event Action<DictationState>? StateChanged;

    public DictationState Current
    {
        get
        {
            lock (_syncRoot)
            {
                return _current;
            }
        }
    }

    public void TransitionTo(DictationState targetState)
    {
        lock (_syncRoot)
        {
            if (!AllowedTransitions[_current].Contains(targetState))
            {
                throw new InvalidOperationException(
                    $"Invalid dictation state transition: {_current} -> {targetState}.");
            }

            _current = targetState;
        }

        StateChanged?.Invoke(targetState);
    }

    private static HashSet<DictationState> Set(params DictationState[] states) => states.ToHashSet();
}
