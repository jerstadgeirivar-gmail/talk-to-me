namespace VoiceType.Core;

public interface IApplicationStateController
{
    event Action<DictationState>? StateChanged;

    DictationState Current { get; }

    void TransitionTo(DictationState targetState);
}
