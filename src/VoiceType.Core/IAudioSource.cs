namespace VoiceType.Core;

public interface IAudioSource : IAsyncDisposable
{
    string Name { get; }

    AudioFormat Format { get; }

    IAsyncEnumerable<AudioFrame> ReadFramesAsync(CancellationToken cancellationToken);
}
