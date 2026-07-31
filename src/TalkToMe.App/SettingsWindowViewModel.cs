using System.ComponentModel;
using System.Runtime.CompilerServices;
using TalkToMe.Core;
using TalkToMe.Infrastructure;

namespace TalkToMe.App;

public sealed class SettingsWindowViewModel(
    IApplicationSettingsStore settingsStore,
    INamedSecretStore secretStore,
    ITranscriptionProviderFactory providerFactory,
    Func<string, CancellationToken, Task> applyProvider) : INotifyPropertyChanged
{
    private string _selectedProviderId = TranscriptionProviderIds.LocalWhisper;
    private string _azureEndpoint = string.Empty, _azureDeployment = string.Empty, _azureApiVersion = "2025-04-01-preview";
    private string _lmStudioBaseUrl = "http://localhost:1234", _lmStudioModel = string.Empty;
    private string _ollamaBaseUrl = "http://localhost:11434", _ollamaModel = string.Empty;
    private string _technicalVocabulary = string.Empty, _hotkey = "Win+<", _targetWindowPolicy = "OriginalTarget", _diagnosticLoggingLevel = "Information";
    private bool _retainFailedAudio = true, _startWithWindows, _clipboardOnlyMode, _voiceCommandsEnabled, _isBusy;
    private string _azureSecretStatus = "Not configured", _lmStudioSecretStatus = "Not configured", _ollamaSecretStatus = "Not configured";
    private string _providerStatus = "Not tested", _statusText = string.Empty, _progressText = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<TranscriptionProviderDescriptor> Providers => providerFactory.Providers;
    public string SelectedProviderId { get => _selectedProviderId; set { if (SetProperty(ref _selectedProviderId, value)) RaiseProviderProperties(); } }
    public TranscriptionProviderDescriptor? SelectedProvider => Providers.FirstOrDefault(provider => provider.Id == SelectedProviderId);
    public bool IsLocalWhisper => SelectedProviderId == TranscriptionProviderIds.LocalWhisper;
    public bool IsAzure => SelectedProviderId == TranscriptionProviderIds.AzureOpenAi;
    public bool IsLmStudio => SelectedProviderId == TranscriptionProviderIds.LmStudio;
    public bool IsOllama => SelectedProviderId == TranscriptionProviderIds.Ollama;
    public string AzureEndpoint { get => _azureEndpoint; set => SetProperty(ref _azureEndpoint, value); }
    public string AzureDeployment { get => _azureDeployment; set => SetProperty(ref _azureDeployment, value); }
    public string AzureApiVersion { get => _azureApiVersion; set => SetProperty(ref _azureApiVersion, value); }
    public string LmStudioBaseUrl { get => _lmStudioBaseUrl; set => SetProperty(ref _lmStudioBaseUrl, value); }
    public string LmStudioModel { get => _lmStudioModel; set => SetProperty(ref _lmStudioModel, value); }
    public string OllamaBaseUrl { get => _ollamaBaseUrl; set => SetProperty(ref _ollamaBaseUrl, value); }
    public string OllamaModel { get => _ollamaModel; set => SetProperty(ref _ollamaModel, value); }
    public string TechnicalVocabulary { get => _technicalVocabulary; set => SetProperty(ref _technicalVocabulary, value); }
    public bool RetainFailedAudio { get => _retainFailedAudio; set => SetProperty(ref _retainFailedAudio, value); }
    public bool StartWithWindows { get => _startWithWindows; set => SetProperty(ref _startWithWindows, value); }
    public bool ClipboardOnlyMode { get => _clipboardOnlyMode; set => SetProperty(ref _clipboardOnlyMode, value); }
    public string TargetWindowPolicy { get => _targetWindowPolicy; set => SetProperty(ref _targetWindowPolicy, value); }
    public string Hotkey { get => _hotkey; set => SetProperty(ref _hotkey, value); }
    public bool VoiceCommandsEnabled { get => _voiceCommandsEnabled; set => SetProperty(ref _voiceCommandsEnabled, value); }
    public string DiagnosticLoggingLevel { get => _diagnosticLoggingLevel; set => SetProperty(ref _diagnosticLoggingLevel, value); }
    public string AzureSecretStatus { get => _azureSecretStatus; private set => SetProperty(ref _azureSecretStatus, value); }
    public string LmStudioSecretStatus { get => _lmStudioSecretStatus; private set => SetProperty(ref _lmStudioSecretStatus, value); }
    public string OllamaSecretStatus { get => _ollamaSecretStatus; private set => SetProperty(ref _ollamaSecretStatus, value); }
    public string ProviderStatus { get => _providerStatus; private set => SetProperty(ref _providerStatus, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string ProgressText { get => _progressText; private set => SetProperty(ref _progressText, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        ApplicationSettings settings = await settingsStore.LoadAsync(cancellationToken);
        bool hasAzure = await secretStore.HasSecretAsync(TranscriptionProviderIds.AzureOpenAi, cancellationToken);
        SelectedProviderId = providerFactory.SelectProviderId(settings, hasAzure);
        if (settings.TranscriptionProviderId is null)
        {
            settings = settings with { TranscriptionProviderId = SelectedProviderId };
            await settingsStore.SaveAsync(settings, cancellationToken);
        }
        AzureEndpoint = settings.AzureEndpoint; AzureDeployment = settings.AzureDeployment; AzureApiVersion = settings.AzureApiVersion;
        LmStudioBaseUrl = settings.LmStudioBaseUrl; LmStudioModel = settings.LmStudioModel;
        OllamaBaseUrl = settings.OllamaBaseUrl; OllamaModel = settings.OllamaModel;
        TechnicalVocabulary = settings.TechnicalVocabulary; RetainFailedAudio = settings.RetainFailedAudio;
        StartWithWindows = settings.StartWithWindows; ClipboardOnlyMode = settings.ClipboardOnlyMode;
        TargetWindowPolicy = settings.TargetWindowPolicy; Hotkey = settings.Hotkey;
        VoiceCommandsEnabled = settings.VoiceCommandsEnabled ?? false; DiagnosticLoggingLevel = settings.DiagnosticLoggingLevel;
        AzureSecretStatus = hasAzure ? "Configured" : "Not configured";
        LmStudioSecretStatus = await secretStore.HasSecretAsync(TranscriptionProviderIds.LmStudio, cancellationToken) ? "Configured" : "Not configured";
        OllamaSecretStatus = await secretStore.HasSecretAsync(TranscriptionProviderIds.Ollama, cancellationToken) ? "Configured" : "Not configured";
        ProviderStatus = "Not tested.";
    }

    public async Task<bool> SaveAsync(string azureKey, string lmStudioToken, string ollamaToken,
        Func<string, bool> applyHotkey, Func<bool, bool> applyVoiceCommands, CancellationToken cancellationToken)
    {
        if (!Validate() || !applyHotkey(Hotkey.Trim())) return false;
        await SaveConfigurationAsync(cancellationToken);
        await SaveSecretAsync(TranscriptionProviderIds.AzureOpenAi, azureKey, value => AzureSecretStatus = value, cancellationToken);
        await SaveSecretAsync(TranscriptionProviderIds.LmStudio, lmStudioToken, value => LmStudioSecretStatus = value, cancellationToken);
        await SaveSecretAsync(TranscriptionProviderIds.Ollama, ollamaToken, value => OllamaSecretStatus = value, cancellationToken);
        await applyProvider(SelectedProviderId, cancellationToken);
        bool voiceReady = applyVoiceCommands(VoiceCommandsEnabled);
        StatusText = voiceReady ? "Settings saved. Provider changes apply to the next transcription."
            : "Settings saved, but no local English Windows speech recognizer is available for voice commands.";
        return true;
    }

    public async Task TestProviderAsync(CancellationToken cancellationToken)
    {
        if (!ValidateProvider()) return;
        IsBusy = true; ProviderStatus = "Testing…";
        try
        {
            await SaveConfigurationAsync(cancellationToken);
            ProviderTestResult result = await providerFactory.TestAsync(SelectedProviderId, cancellationToken);
            ProviderStatus = result.Message;
        }
        catch (OperationCanceledException) { ProviderStatus = "Provider test cancelled."; }
        finally { IsBusy = false; }
    }

    public async Task RepairModelAsync(CancellationToken cancellationToken)
    {
        IsBusy = true; ProviderStatus = "Downloading and verifying the pinned model…";
        Progress<double> progress = new(value => ProgressText = $"{value:N0}%");
        try
        {
            await LocalWhisperModel.RepairAsync(progress, cancellationToken);
            ProviderStatus = (await LocalWhisperModel.VerifyAsync(cancellationToken)).Message;
        }
        catch (OperationCanceledException) { ProviderStatus = "Model repair cancelled."; }
        catch (Exception exception) { ProviderStatus = $"Model repair failed: {exception.Message}"; }
        finally { IsBusy = false; ProgressText = string.Empty; }
    }

    public async Task RemoveSecretAsync(string providerId, CancellationToken cancellationToken)
    {
        await secretStore.RemoveSecretAsync(providerId, cancellationToken);
        if (providerId == TranscriptionProviderIds.AzureOpenAi) AzureSecretStatus = "Not configured";
        else if (providerId == TranscriptionProviderIds.LmStudio) LmStudioSecretStatus = "Not configured";
        else if (providerId == TranscriptionProviderIds.Ollama) OllamaSecretStatus = "Not configured";
        StatusText = "Protected credential removed.";
    }

    private async Task SaveConfigurationAsync(CancellationToken cancellationToken) => await settingsStore.SaveAsync(new ApplicationSettings
    {
        TranscriptionProviderId = SelectedProviderId,
        LocalWhisperModel = "small-q5_1",
        AzureEndpoint = AzureEndpoint.Trim(),
        AzureDeployment = AzureDeployment.Trim(),
        AzureApiVersion = AzureApiVersion.Trim(),
        LmStudioBaseUrl = LmStudioBaseUrl.Trim(),
        LmStudioModel = LmStudioModel.Trim(),
        OllamaBaseUrl = OllamaBaseUrl.Trim(),
        OllamaModel = OllamaModel.Trim(),
        TechnicalVocabulary = TechnicalVocabulary,
        RetainFailedAudio = RetainFailedAudio,
        StartWithWindows = StartWithWindows,
        ClipboardOnlyMode = ClipboardOnlyMode,
        TargetWindowPolicy = TargetWindowPolicy,
        Hotkey = Hotkey.Trim(),
        VoiceCommandsEnabled = VoiceCommandsEnabled,
        DiagnosticLoggingLevel = DiagnosticLoggingLevel,
    }, cancellationToken);

    private async Task SaveSecretAsync(string id, string value, Action<string> update, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        await secretStore.SetSecretAsync(id, value, token); update("Configured");
    }

    private bool Validate()
    {
        if (!HotkeyGesture.TryParse(Hotkey, out _)) { StatusText = "Enter a valid hotkey such as Win+< or Ctrl+Alt+F9."; return false; }
        return ValidateProvider();
    }

    private bool ValidateProvider()
    {
        StatusText = string.Empty;
        try
        {
            if (IsAzure)
            {
                if (!Uri.TryCreate(AzureEndpoint, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps)
                    throw new ArgumentException("The Azure endpoint must be a valid HTTPS address.");
                if (string.IsNullOrWhiteSpace(AzureDeployment)) throw new ArgumentException("Azure deployment is required.");
            }
            if (IsLmStudio) TranscriptionProviderRegistry.ValidateServerUri(LmStudioBaseUrl, "LM Studio");
            if (IsOllama) TranscriptionProviderRegistry.ValidateServerUri(OllamaBaseUrl, "Ollama");
            return true;
        }
        catch (Exception exception) { StatusText = exception.Message; return false; }
    }

    private void RaiseProviderProperties()
    {
        OnPropertyChanged(nameof(SelectedProvider)); OnPropertyChanged(nameof(IsLocalWhisper)); OnPropertyChanged(nameof(IsAzure));
        OnPropertyChanged(nameof(IsLmStudio)); OnPropertyChanged(nameof(IsOllama)); ProviderStatus = "Not tested";
    }
    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; OnPropertyChanged(name); return true; }
    private void OnPropertyChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
