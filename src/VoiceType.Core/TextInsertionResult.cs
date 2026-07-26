namespace VoiceType.Core;

public sealed record TextInsertionResult(
    bool Inserted,
    bool TranscriptLeftOnClipboard,
    string Message);
