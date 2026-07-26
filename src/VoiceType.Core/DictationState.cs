namespace VoiceType.Core;

public enum DictationState
{
    Idle,
    StartingRecording,
    Recording,
    StoppingRecording,
    PreparingAudio,
    Transcribing,
    ReadyToInsert,
    Inserting,
    Completed,
    Cancelling,
    Cancelled,
    RecoverableFailure,
    FatalFailure,
}
