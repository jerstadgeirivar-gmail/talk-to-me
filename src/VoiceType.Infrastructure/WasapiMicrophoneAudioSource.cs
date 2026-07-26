using System.Runtime.CompilerServices;
using System.Threading.Channels;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using VoiceType.Core;

namespace VoiceType.Infrastructure;

public sealed class WasapiMicrophoneAudioSource : IAudioSource
{
    private const int FrameDurationMilliseconds = 100;
    private int _isActive;

    public string Name => "Default microphone";

    public AudioFormat Format => AudioFormat.SpeechPcm;

    public async IAsyncEnumerable<AudioFrame> ReadFramesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _isActive, 1) != 0)
        {
            throw new InvalidOperationException("A microphone capture session is already active.");
        }

        using WasapiCapture capture = new();
        BufferedWaveProvider bufferedProvider = new(capture.WaveFormat)
        {
            DiscardOnBufferOverflow = false,
            ReadFully = false,
        };
        ISampleProvider monoProvider = ToMono(bufferedProvider.ToSampleProvider());
        WdlResamplingSampleProvider resampledProvider = new(monoProvider, Format.SampleRate);
        SampleToWaveProvider16 outputProvider = new(resampledProvider);
        Channel<bool> dataAvailable = Channel.CreateUnbounded<bool>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

        EventHandler<WaveInEventArgs> onDataAvailable = (_, eventArgs) =>
        {
            bufferedProvider.AddSamples(eventArgs.Buffer, 0, eventArgs.BytesRecorded);
            dataAvailable.Writer.TryWrite(true);
        };
        EventHandler<StoppedEventArgs> onRecordingStopped = (_, eventArgs) =>
            dataAvailable.Writer.TryComplete(eventArgs.Exception);

        capture.DataAvailable += onDataAvailable;
        capture.RecordingStopped += onRecordingStopped;

        int frameSize = Format.BytesPerSecond * FrameDurationMilliseconds / 1_000;
        byte[] readBuffer = new byte[frameSize];
        TimeSpan position = TimeSpan.Zero;

        try
        {
            capture.StartRecording();
            await foreach (bool _ in dataAvailable.Reader.ReadAllAsync(cancellationToken))
            {
                int bytesRead = outputProvider.Read(readBuffer, 0, readBuffer.Length);
                if (bytesRead == 0)
                {
                    continue;
                }

                byte[] frameData = readBuffer.AsSpan(0, bytesRead).ToArray();
                yield return new AudioFrame(frameData, position);
                position += TimeSpan.FromSeconds((double)bytesRead / Format.BytesPerSecond);
            }
        }
        finally
        {
            capture.DataAvailable -= onDataAvailable;
            capture.RecordingStopped -= onRecordingStopped;
            if (capture.CaptureState != CaptureState.Stopped)
            {
                capture.StopRecording();
            }

            Interlocked.Exchange(ref _isActive, 0);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static ISampleProvider ToMono(ISampleProvider source)
    {
        return source.WaveFormat.Channels switch
        {
            1 => source,
            2 => new StereoToMonoSampleProvider(source),
            _ => throw new NotSupportedException(
                $"Microphone input with {source.WaveFormat.Channels} channels is not supported."),
        };
    }
}
