using TalkToMe.Core;
using TalkToMe.Infrastructure;

namespace TalkToMe.Infrastructure.Tests;

public sealed class ClipboardTextInsertionServiceTests
{
    private static readonly WindowTarget Target = new((nint)123, 456, "Editor");

    [Fact]
    public async Task RestoresClipboardWhenTargetCannotBeActivated()
    {
        FakeClipboard clipboard = new("previous contents");
        ClipboardTextInsertionService service = new(
            new FakeWindowTargetService(activates: false),
            clipboard,
            new FakeKeyboardInput());

        TextInsertionResult result = await service.InsertAsync(Target, "private transcript", CancellationToken.None);

        Assert.False(result.Inserted);
        Assert.False(result.TranscriptLeftOnClipboard);
        Assert.Equal("previous contents", clipboard.Current);
    }

    [Fact]
    public async Task RestoresClipboardWhenPasteFails()
    {
        FakeClipboard clipboard = new("previous contents");
        ClipboardTextInsertionService service = new(
            new FakeWindowTargetService(activates: true),
            clipboard,
            new FakeKeyboardInput(throws: true));

        TextInsertionResult result = await service.InsertAsync(Target, "private transcript", CancellationToken.None);

        Assert.False(result.Inserted);
        Assert.False(result.TranscriptLeftOnClipboard);
        Assert.Equal("previous contents", clipboard.Current);
    }

    [Fact]
    public void FailedAudioRetentionDefaultsToOff()
    {
        Assert.False(new ApplicationSettings().RetainFailedAudio);
    }

    private sealed class FakeClipboard(object? initial) : IClipboardAdapter
    {
        public object? Current { get; private set; } = initial;

        public object? CaptureData() => Current;

        public void SetText(string text) => Current = text;

        public bool ContainsText(string text) => Equals(Current, text);

        public void RestoreData(object? data) => Current = data;
    }

    private sealed class FakeKeyboardInput(bool throws = false) : IKeyboardInputAdapter
    {
        public void Paste()
        {
            if (throws)
            {
                throw new InvalidOperationException("Synthetic paste failure.");
            }
        }
    }

    private sealed class FakeWindowTargetService(bool activates) : IWindowTargetService
    {
        public WindowTarget CaptureForegroundTarget() => Target;

        public bool IsValid(WindowTarget target) => true;

        public Task<bool> ActivateAsync(WindowTarget target, CancellationToken cancellationToken) =>
            Task.FromResult(activates);
    }
}
