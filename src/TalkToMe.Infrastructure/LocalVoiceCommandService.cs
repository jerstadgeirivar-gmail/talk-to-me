using System.Globalization;
using System.Speech.Recognition;

namespace TalkToMe.Infrastructure;

public sealed class LocalVoiceCommandService : IDisposable
{
    private const string Keyword = "computer";
    private const float MinimumConfidence = 0.72f;
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(1.25);
    private SpeechRecognitionEngine? _recognizer;
    private DateTime _lastDetectionUtc = DateTime.MinValue;
    private bool _disposed;

    public event EventHandler<VoiceCommandDetectedEventArgs>? CommandDetected;

    public bool IsListening => _recognizer is not null;

    public bool Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_recognizer is not null)
        {
            return true;
        }

        SpeechRecognitionEngine? recognizer = null;
        try
        {
            RecognizerInfo? englishRecognizer = SpeechRecognitionEngine
                .InstalledRecognizers()
                .FirstOrDefault(info => info.Culture.Name.Equals("en-US", StringComparison.OrdinalIgnoreCase))
                ?? SpeechRecognitionEngine
                    .InstalledRecognizers()
                    .FirstOrDefault(info => info.Culture.TwoLetterISOLanguageName == "en");
            if (englishRecognizer is null)
            {
                return false;
            }

            recognizer = new SpeechRecognitionEngine(englishRecognizer);
            GrammarBuilder grammarBuilder = new(Keyword)
            {
                Culture = englishRecognizer.Culture,
            };
            recognizer.LoadGrammar(new Grammar(grammarBuilder) { Name = "TalkToMe voice command" });
            recognizer.SpeechRecognized += OnSpeechRecognized;
            recognizer.SetInputToDefaultAudioDevice();
            recognizer.RecognizeAsync(RecognizeMode.Multiple);
            _recognizer = recognizer;
            return true;
        }
        catch (InvalidOperationException)
        {
            recognizer?.Dispose();
            return false;
        }
    }

    public void Stop()
    {
        SpeechRecognitionEngine? recognizer = Interlocked.Exchange(ref _recognizer, null);
        if (recognizer is null)
        {
            return;
        }

        recognizer.SpeechRecognized -= OnSpeechRecognized;
        try
        {
            recognizer.RecognizeAsyncCancel();
            recognizer.SetInputToNull();
        }
        catch (InvalidOperationException)
        {
            // The recognizer may already be stopping after an audio-device change.
        }
        finally
        {
            recognizer.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs eventArgs)
    {
        if (eventArgs.Result.Confidence < MinimumConfidence ||
            !eventArgs.Result.Text.Equals(Keyword, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        if (now - _lastDetectionUtc < MinimumInterval)
        {
            return;
        }

        _lastDetectionUtc = now;
        TimeSpan trailingAudio = eventArgs.Result.Audio.Duration + TimeSpan.FromMilliseconds(300);
        if (sender is SpeechRecognitionEngine recognizer)
        {
            TimeSpan recognitionLag =
                recognizer.AudioPosition -
                (eventArgs.Result.Audio.AudioPosition + eventArgs.Result.Audio.Duration);
            if (recognitionLag > TimeSpan.Zero && recognitionLag < TimeSpan.FromSeconds(2))
            {
                trailingAudio += recognitionLag;
            }
        }

        trailingAudio = TimeSpan.FromMilliseconds(
            Math.Clamp(trailingAudio.TotalMilliseconds, 500, 2_500));
        CommandDetected?.Invoke(
            this,
            new VoiceCommandDetectedEventArgs(Keyword, trailingAudio, eventArgs.Result.Confidence));
    }
}

public sealed class VoiceCommandDetectedEventArgs(
    string command,
    TimeSpan trailingAudio,
    float confidence) : EventArgs
{
    public string Command { get; } = command;

    public TimeSpan TrailingAudio { get; } = trailingAudio;

    public float Confidence { get; } = confidence;
}
