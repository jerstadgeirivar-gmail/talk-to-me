using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using TalkToMe.Core;
using TalkToMe.Infrastructure;

namespace TalkToMe.App;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly IAudioSource _audioSource;
    private readonly IAudioRecordingService _recordingService;
    private readonly IApplicationStateController _stateController;
    private readonly ITranscriptionProvider? _transcriptionProvider;
    private readonly IWindowTargetService _windowTargetService;
    private readonly ITextInsertionService _textInsertionService;
    private readonly string _outputPath;
    private readonly bool _allowRecordOnly;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly AsyncDelegateCommand _startRecordingCommand;
    private readonly AsyncDelegateCommand _stopRecordingCommand;
    private readonly AsyncDelegateCommand _insertCommand;
    private readonly AsyncDelegateCommand _cancelCommand;
    private readonly AsyncDelegateCommand _retryCommand;
    private readonly AsyncDelegateCommand _deletePendingCommand;
    private string _statusText = "Ready";
    private string _durationText = "00:00:00.0";
    private double _inputLevel;
    private bool _isRecording;
    private bool _insertAfterTranscription;
    private string _transcriptText = string.Empty;
    private WindowTarget? _windowTarget;
    private RecordingResult? _lastRecording;
    private string _targetText = "No target captured — focus a text field and use the hotkey";

    public MainWindowViewModel(
        IAudioSource audioSource,
        IAudioRecordingService recordingService,
        IApplicationStateController stateController,
        ITranscriptionProvider? transcriptionProvider,
        IWindowTargetService windowTargetService,
        ITextInsertionService textInsertionService,
        IGlobalHotkeyService globalHotkeyService,
        string outputPath,
        bool allowRecordOnly,
        RecoveredRecording? recoveredRecording = null)
    {
        _audioSource = audioSource;
        _recordingService = recordingService;
        _stateController = stateController;
        _transcriptionProvider = transcriptionProvider;
        _windowTargetService = windowTargetService;
        _textInsertionService = textInsertionService;
        _outputPath = outputPath;
        _allowRecordOnly = allowRecordOnly;
        _startRecordingCommand = new AsyncDelegateCommand(StartRecordingFromWindowAsync, CanStartRecording);
        _stopRecordingCommand = new AsyncDelegateCommand(StopRecordingAsync, () => IsRecording);
        _insertCommand = new AsyncDelegateCommand(InsertTranscriptAsync, CanInsertTranscript);
        _cancelCommand = new AsyncDelegateCommand(CancelRecordingAsync, () => IsRecording);
        _retryCommand = new AsyncDelegateCommand(RetryTranscriptionAsync, CanRetryTranscription);
        _deletePendingCommand = new AsyncDelegateCommand(DeletePendingAudioAsync, CanDeletePendingAudio);
        if (recoveredRecording is not null)
        {
            _lastRecording = recoveredRecording.Recording;
            _windowTarget = recoveredRecording.Target is not null &&
                            _windowTargetService.IsValid(recoveredRecording.Target)
                ? recoveredRecording.Target
                : null;
            UpdateTargetText();
            _stateController.TransitionTo(DictationState.RecoverableFailure);
            _statusText = $"Recovered {FormatDuration(_lastRecording.Duration)} of audio after an interrupted session";
        }
        else if (_transcriptionProvider is null && !_allowRecordOnly)
        {
            _statusText = "Azure setup is missing";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string DurationText
    {
        get => _durationText;
        private set => SetProperty(ref _durationText, value);
    }

    public string SourceText => _audioSource.Name;

    public string TargetText
    {
        get => _targetText;
        private set => SetProperty(ref _targetText, value);
    }

    public double InputLevel
    {
        get => _inputLevel;
        private set => SetProperty(ref _inputLevel, value);
    }

    public bool IsRecording
    {
        get => _isRecording;
        private set
        {
            if (SetProperty(ref _isRecording, value))
            {
                _startRecordingCommand.RaiseCanExecuteChanged();
                _stopRecordingCommand.RaiseCanExecuteChanged();
                _cancelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string TranscriptText
    {
        get => _transcriptText;
        private set => SetProperty(ref _transcriptText, value);
    }

    public ICommand StartRecordingCommand => _startRecordingCommand;

    public ICommand StopRecordingCommand => _stopRecordingCommand;

    public ICommand InsertCommand => _insertCommand;

    public ICommand CancelCommand => _cancelCommand;

    public ICommand RetryCommand => _retryCommand;

    public ICommand DeletePendingCommand => _deletePendingCommand;

    public void ToggleRecording(bool insertAfterTranscription = false)
    {
        if (_stateController.Current == DictationState.ReadyToInsert &&
            !string.IsNullOrWhiteSpace(TranscriptText))
        {
            _windowTarget = TryCaptureForegroundTarget();
            UpdateTargetText();
            if (CanInsertTranscript())
            {
                _insertCommand.Execute(null);
            }
        }
        else if (_stopRecordingCommand.CanExecute(null))
        {
            _stopRecordingCommand.Execute(null);
        }
        else if (_startRecordingCommand.CanExecute(null))
        {
            _insertAfterTranscription = insertAfterTranscription;
            _ = StartRecordingAsync();
        }
    }

    public void ShowHotkeyRegistrationFailure(string hotkey)
    {
        StatusText = $"The {hotkey} shortcut is already in use";
    }

    public void CopyTranscriptToClipboard()
    {
        if (!string.IsNullOrWhiteSpace(TranscriptText))
        {
            System.Windows.Clipboard.SetText(TranscriptText, System.Windows.TextDataFormat.UnicodeText);
            StatusText = "Transcript copied";
        }
    }

    public async ValueTask DisposeAsync()
    {
        _lifetimeCancellation.Cancel();
        await _recordingService.DisposeAsync();
        await _audioSource.DisposeAsync();
        if (_transcriptionProvider is not null)
        {
            await _transcriptionProvider.DisposeAsync();
        }

        _lifetimeCancellation.Dispose();
    }

    private bool CanStartRecording() =>
        (_transcriptionProvider is not null || _allowRecordOnly) &&
        !IsRecording &&
        _stateController.Current is DictationState.Idle or DictationState.Completed;

    private bool CanInsertTranscript() =>
        _stateController.Current == DictationState.ReadyToInsert &&
        _windowTarget is not null &&
        !string.IsNullOrWhiteSpace(TranscriptText);

    private Task StartRecordingFromWindowAsync()
    {
        _insertAfterTranscription = false;
        return StartRecordingAsync();
    }

    private async Task StartRecordingAsync()
    {
        try
        {
            if (_stateController.Current == DictationState.Completed)
            {
                _stateController.TransitionTo(DictationState.Idle);
            }

            _stateController.TransitionTo(DictationState.StartingRecording);
            StatusText = "Starting recording";
            DurationText = "00:00:00.0";
            InputLevel = 0;
            TranscriptText = string.Empty;
            _windowTarget = _transcriptionProvider is null
                ? null
                : TryCaptureForegroundTarget();
            UpdateTargetText();
            PendingRecordingRecovery.SaveTarget(_outputPath, _windowTarget);

            Progress<RecordingProgress> progress = new(UpdateProgress);
            await _recordingService.StartAsync(
                _audioSource,
                _outputPath,
                progress,
                _lifetimeCancellation.Token);

            _stateController.TransitionTo(DictationState.Recording);
            IsRecording = true;
            StatusText = "Recording";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            TransitionToFailure();
        }
    }

    private async Task StopRecordingAsync()
    {
        try
        {
            _stateController.TransitionTo(DictationState.StoppingRecording);
            IsRecording = false;
            StatusText = "Stopping recording";
            RecordingResult result = await _recordingService.StopAsync(_lifetimeCancellation.Token);
            _lastRecording = result;
            _stateController.TransitionTo(DictationState.PreparingAudio);
            DurationText = FormatDuration(result.Duration);
            InputLevel = 0;
            if (_transcriptionProvider is null)
            {
                _stateController.TransitionTo(DictationState.Completed);
                StatusText = "Audio recording ready";
            }
            else
            {
                _stateController.TransitionTo(DictationState.Transcribing);
                StatusText = "Transcribing";
                await TranscribeRecordingAsync(result);
                if (_insertAfterTranscription && CanInsertTranscript())
                {
                    await InsertTranscriptAsync();
                }
            }

            _startRecordingCommand.RaiseCanExecuteChanged();
        }
        catch (TranscriptionException exception)
        {
            TransitionToFailure(GetTranscriptionFailureMessage(exception.Category));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            TransitionToFailure();
        }
    }

    private void UpdateProgress(RecordingProgress progress)
    {
        DurationText = FormatDuration(progress.Duration);
        InputLevel = Math.Clamp(progress.PeakLevel * 100, 0, 100);
    }

    private async Task InsertTranscriptAsync()
    {
        if (_windowTarget is null)
        {
            TransitionToFailure("The target window is no longer available");
            return;
        }

        _stateController.TransitionTo(DictationState.Inserting);
        StatusText = "Inserting text";
        TextInsertionResult result = await _textInsertionService.InsertAsync(
            _windowTarget,
            TranscriptText,
            _lifetimeCancellation.Token);
        if (result.Inserted)
        {
            _stateController.TransitionTo(DictationState.Completed);
            StatusText = "Text inserted";
            PendingRecordingRecovery.Delete(_lastRecording?.FilePath ?? _outputPath);
            _lastRecording = null;
            _insertAfterTranscription = false;
            _startRecordingCommand.RaiseCanExecuteChanged();
        }
        else
        {
            _stateController.TransitionTo(DictationState.RecoverableFailure);
            StatusText = result.Message;
        }

        _insertCommand.RaiseCanExecuteChanged();
        RaiseRecoveryCanExecuteChanged();
    }

    private async Task CancelRecordingAsync()
    {
        _stateController.TransitionTo(DictationState.Cancelling);
        IsRecording = false;
        StatusText = "Cancelling recording";
        RecordingResult result = await _recordingService.StopAsync(_lifetimeCancellation.Token);
        PendingRecordingRecovery.Delete(result.FilePath);
        _lastRecording = null;
        _insertAfterTranscription = false;
        _stateController.TransitionTo(DictationState.Cancelled);
        _stateController.TransitionTo(DictationState.Idle);
        DurationText = "00:00:00.0";
        InputLevel = 0;
        StatusText = "Recording cancelled";
        _startRecordingCommand.RaiseCanExecuteChanged();
        RaiseRecoveryCanExecuteChanged();
    }

    private bool CanRetryTranscription() =>
        _transcriptionProvider is not null &&
        _lastRecording is not null &&
        _stateController.Current == DictationState.RecoverableFailure;

    private async Task RetryTranscriptionAsync()
    {
        if (_lastRecording is null)
        {
            return;
        }

        _stateController.TransitionTo(DictationState.Transcribing);
        StatusText = "Retrying transcription";
        try
        {
            await TranscribeRecordingAsync(_lastRecording);
        }
        catch (TranscriptionException exception)
        {
            TransitionToFailure(GetTranscriptionFailureMessage(exception.Category));
        }
    }

    private bool CanDeletePendingAudio() =>
        !IsRecording && _lastRecording is not null && File.Exists(_lastRecording.FilePath);

    private Task DeletePendingAudioAsync()
    {
        if (_lastRecording is not null)
        {
            PendingRecordingRecovery.Delete(_lastRecording.FilePath);
            _lastRecording = null;
        }

        _insertAfterTranscription = false;
        if (_stateController.Current == DictationState.RecoverableFailure)
        {
            _stateController.TransitionTo(DictationState.Idle);
        }

        StatusText = "Pending audio deleted";
        _startRecordingCommand.RaiseCanExecuteChanged();
        RaiseRecoveryCanExecuteChanged();
        return Task.CompletedTask;
    }

    private async Task TranscribeRecordingAsync(RecordingResult recording)
    {
        TranscriptionResult transcription = await _transcriptionProvider!.TranscribeAsync(
            new RecordedAudio(recording.FilePath, recording.Duration, recording.FileSizeBytes),
            new TranscriptionContext("no", null),
            _lifetimeCancellation.Token);
        TranscriptText = transcription.Text;
        _stateController.TransitionTo(DictationState.ReadyToInsert);
        StatusText = _windowTarget is null
            ? "Transcript ready — copy it or use the shortcut from another app to insert"
            : "Transcript ready";
        _insertCommand.RaiseCanExecuteChanged();
        RaiseRecoveryCanExecuteChanged();
    }

    private void RaiseRecoveryCanExecuteChanged()
    {
        _retryCommand.RaiseCanExecuteChanged();
        _deletePendingCommand.RaiseCanExecuteChanged();
    }

    private WindowTarget? TryCaptureForegroundTarget()
    {
        try
        {
            return _windowTargetService.CaptureForegroundTarget();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private void UpdateTargetText()
    {
        TargetText = _windowTarget is null
            ? "No target captured — focus a text field and use the hotkey"
            : $"{_windowTarget.Title}  •  PID {_windowTarget.ProcessId}  •  HWND 0x{_windowTarget.Handle:X}";
    }

    private void TransitionToFailure(string message = "Recording failed")
    {
        DictationState current = _stateController.Current;
        if (current is DictationState.StartingRecording or
            DictationState.Recording or
            DictationState.StoppingRecording or
            DictationState.PreparingAudio or
            DictationState.Transcribing)
        {
            _stateController.TransitionTo(DictationState.RecoverableFailure);
        }

        IsRecording = false;
        StatusText = message;
        _insertCommand.RaiseCanExecuteChanged();
        RaiseRecoveryCanExecuteChanged();
    }

    private static string GetTranscriptionFailureMessage(TranscriptionFailureCategory category) => category switch
    {
        TranscriptionFailureCategory.Authentication => "Azure rejected the API key",
        TranscriptionFailureCategory.Authorization => "Access to the deployment was denied",
        TranscriptionFailureCategory.DeploymentNotFound => "The deployment was not found",
        TranscriptionFailureCategory.RateLimited => "Azure is busy. Try again later",
        TranscriptionFailureCategory.RequestTooLarge => "The recording is too large",
        TranscriptionFailureCategory.Network => "Unable to reach Azure",
        TranscriptionFailureCategory.Timeout => "Azure timed out",
        _ => "Transcription failed",
    };

    private static string FormatDuration(TimeSpan duration) =>
        duration.ToString(@"hh\:mm\:ss\.f", CultureInfo.InvariantCulture);

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private sealed class AsyncDelegateCommand(Func<Task> execute, Func<bool> canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => canExecute();

        public async void Execute(object? parameter) => await execute();

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
