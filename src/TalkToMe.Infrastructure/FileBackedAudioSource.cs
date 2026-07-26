using System.Runtime.CompilerServices;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using TalkToMe.Core;

namespace TalkToMe.Infrastructure;

public sealed class FileBackedAudioSource(string filePath, double playbackSpeed = 1) : IAudioSource
{
    private const int FrameDurationMilliseconds = 100;

    private readonly string _filePath = Path.GetFullPath(filePath);
    private readonly double _playbackSpeed = playbackSpeed > 0
        ? playbackSpeed
        : throw new ArgumentOutOfRangeException(nameof(playbackSpeed));

    public string Name => "Diagnostic audio file";

    public AudioFormat Format => AudioFormat.SpeechPcm;

    public async IAsyncEnumerable<AudioFrame> ReadFramesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using AudioFileReader reader = new(_filePath);
        ISampleProvider monoProvider = ToMono(reader);
        WdlResamplingSampleProvider resampledProvider = new(monoProvider, Format.SampleRate);
        SampleToWaveProvider16 waveProvider = new(resampledProvider);
        int frameSize = Format.BytesPerSecond * FrameDurationMilliseconds / 1_000;
        byte[] readBuffer = new byte[frameSize];
        TimeSpan position = TimeSpan.Zero;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int bytesRead = waveProvider.Read(readBuffer, 0, readBuffer.Length);
            if (bytesRead == 0)
            {
                yield break;
            }

            byte[] frameData = readBuffer.AsSpan(0, bytesRead).ToArray();
            yield return new AudioFrame(frameData, position);

            TimeSpan frameDuration = TimeSpan.FromSeconds((double)bytesRead / Format.BytesPerSecond);
            position += frameDuration;
            await Task.Delay(frameDuration / _playbackSpeed, cancellationToken);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static ISampleProvider ToMono(AudioFileReader source)
    {
        return source.WaveFormat.Channels switch
        {
            1 => source,
            2 => new StereoToMonoSampleProvider(source),
            _ => throw new NotSupportedException(
                $"Diagnostic audio with {source.WaveFormat.Channels} channels is not supported."),
        };
    }
}
