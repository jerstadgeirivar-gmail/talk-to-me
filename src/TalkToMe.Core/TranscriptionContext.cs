namespace TalkToMe.Core;

public sealed record TranscriptionContext(
	string? Language,
	string? Prompt,
	IProgress<string>? Progress = null);
