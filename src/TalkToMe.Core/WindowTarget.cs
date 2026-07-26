namespace TalkToMe.Core;

public sealed record WindowTarget(nint Handle, int ProcessId, string Title);
