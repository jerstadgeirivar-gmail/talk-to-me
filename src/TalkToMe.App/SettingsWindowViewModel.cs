using System.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;
using TalkToMe.Core;
using TalkToMe.Infrastructure;

namespace TalkToMe.App;

public sealed class SettingsWindowViewModel(
    IApplicationSettingsStore settingsStore,
    INamedSecretStore secretStore,
    ITranscriptionProviderFactory providerFactory,
    Func<string, CancellationToken, Task> applyProvider,
    Action<bool> applyFailedAudioRetention) : INotifyPropertyChanged
{
    private string _selectedProviderId = TranscriptionProviderIds.LocalWhisper;
    private string _azureEndpoint = string.Empty, _azureDeployment = string.Empty, _azureApiVersion = "2025-04-01-preview";
    private string _lmStudioBaseUrl = "http://localhost:1234", _lmStudioModel = string.Empty;
    private string _ollamaBaseUrl = "http://localhost:11434", _ollamaModel = string.Empty;
    private string _transcriptionLanguageMode = TranscriptionLanguageModes.Norwegian;
    private string _hotkey = "Win+<", _targetWindowPolicy = "OriginalTarget", _diagnosticLoggingLevel = "Information";
    private bool _retainFailedAudio, _startWithWindows, _clipboardOnlyMode, _voiceCommandsEnabled, _isBusy;
    private string _azureSecretStatus = "Not configured", _lmStudioSecretStatus = "Not configured", _ollamaSecretStatus = "Not configured";
    private string _providerStatus = "Not tested", _statusText = string.Empty, _progressText = string.Empty;
    private string? _selectedEndpointId;
    private string _endpointName = "New endpoint";
    private int _maximumOutputTokens = 2000, _estimatedTokensPerMinute = 600, _maximumSegmentSeconds = 300, _requestTimeoutSeconds = 600;
    private long _maximumUploadBytes = 24_000_000;
    private bool _openAiCompatibleRoute, _bearerAuthentication, _retainProgress, _changingEndpoint;
    private readonly List<string> _removedCredentialIds = [];

    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<TranscriptionProviderDescriptor> Providers => providerFactory.Providers;
    public ObservableCollection<TranscriptionEndpointItem> EndpointProfiles { get; } = [];
    public bool CanRemoveEndpoint => EndpointProfiles.Count > 1;
    public string? SelectedEndpointId
    {
        get => _selectedEndpointId;
        set
        {
            if (_changingEndpoint || value == _selectedEndpointId) return;
            _changingEndpoint = true;
            try
            {
                CaptureEndpointDraft();
                _selectedEndpointId = value;
                TranscriptionEndpointProfile? selected = EndpointProfiles.FirstOrDefault(profile => profile.Id == value)?.Profile;
                if (selected is not null)
                {
                    EndpointName = selected.Name; AzureEndpoint = selected.Endpoint; AzureDeployment = selected.Deployment; AzureApiVersion = selected.ApiVersion;
                    MaximumOutputTokens = selected.MaximumOutputTokens; EstimatedTokensPerMinute = selected.EstimatedTokensPerMinute;
                    MaximumSegmentSeconds = selected.MaximumSegmentSeconds; MaximumUploadBytes = selected.MaximumUploadBytes;
                    RequestTimeoutSeconds = selected.RequestTimeoutSeconds; OpenAiCompatibleRoute = selected.OpenAiCompatibleRoute;
                    BearerAuthentication = selected.BearerAuthentication; RetainProgress = selected.RetainProgress;
                }
                OnPropertyChanged(nameof(SelectedEndpointId));
                OnPropertyChanged(nameof(ActiveEndpointCredentialId));
                ProviderStatus = "Not tested";
                _ = RefreshEndpointCredentialAsync();
            }
            finally { _changingEndpoint = false; }
        }
    }
    public string ActiveEndpointCredentialId => EndpointProfiles.FirstOrDefault(profile => profile.Id == SelectedEndpointId)?.CredentialId
        ?? TranscriptionProviderIds.AzureOpenAi;
    public string EndpointName { get => _endpointName; set => SetProperty(ref _endpointName, value); }
    public int MaximumOutputTokens { get => _maximumOutputTokens; set { if (SetProperty(ref _maximumOutputTokens, value)) OnPropertyChanged(nameof(SegmentPlanText)); } }
    public int EstimatedTokensPerMinute { get => _estimatedTokensPerMinute; set { if (SetProperty(ref _estimatedTokensPerMinute, value)) OnPropertyChanged(nameof(SegmentPlanText)); } }
    public int MaximumSegmentSeconds { get => _maximumSegmentSeconds; set { if (SetProperty(ref _maximumSegmentSeconds, value)) OnPropertyChanged(nameof(SegmentPlanText)); } }
    public long MaximumUploadBytes { get => _maximumUploadBytes; set { if (SetProperty(ref _maximumUploadBytes, value)) OnPropertyChanged(nameof(SegmentPlanText)); } }
    public int RequestTimeoutSeconds { get => _requestTimeoutSeconds; set => SetProperty(ref _requestTimeoutSeconds, value); }
    public bool OpenAiCompatibleRoute { get => _openAiCompatibleRoute; set => SetProperty(ref _openAiCompatibleRoute, value); }
    public bool BearerAuthentication { get => _bearerAuthentication; set => SetProperty(ref _bearerAuthentication, value); }
    public bool RetainProgress { get => _retainProgress; set => SetProperty(ref _retainProgress, value); }
    public string SegmentPlanText => EstimatedTokensPerMinute <= 0 ? "Invalid limits" :
        $"{Math.Max(0, Math.Min(Math.Min(MaximumSegmentSeconds, MaximumOutputTokens * 0.75 / EstimatedTokensPerMinute * 60), (MaximumUploadBytes - 4096) / 32000d)):N1} seconds";
    public IReadOnlyList<TranscriptionLanguageOption> TranscriptionLanguages { get; } =
    [
        new(TranscriptionLanguageModes.Auto, "Auto"),
        new(TranscriptionLanguageModes.Norwegian, "Norwegian"),
        new(TranscriptionLanguageModes.NorwegianEnglish, "Norwegian + English"),
    ];
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
    public string TranscriptionLanguageMode { get => _transcriptionLanguageMode; set => SetProperty(ref _transcriptionLanguageMode, value); }
    public string VocabularyFilePath => settingsStore.VocabularyFilePath
        ?? throw new InvalidOperationException("The settings store does not support vocabulary files.");
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
        foreach (TranscriptionEndpointProfile profile in settings.TranscriptionEndpoints) EndpointProfiles.Add(new TranscriptionEndpointItem(profile));
        if (EndpointProfiles.Count == 0) EndpointProfiles.Add(new TranscriptionEndpointItem(new TranscriptionEndpointProfile { Id = TranscriptionEndpointProfile.LegacyId, Name = "Azure endpoint" }));
        SelectedEndpointId = settings.SelectedTranscriptionEndpointId ?? EndpointProfiles[0].Id;
        OnPropertyChanged(nameof(CanRemoveEndpoint));
        LmStudioBaseUrl = settings.LmStudioBaseUrl; LmStudioModel = settings.LmStudioModel;
        OllamaBaseUrl = settings.OllamaBaseUrl; OllamaModel = settings.OllamaModel;
        TranscriptionLanguageMode = settings.TranscriptionLanguageMode;
        RetainFailedAudio = settings.RetainFailedAudio;
        StartWithWindows = settings.StartWithWindows; ClipboardOnlyMode = settings.ClipboardOnlyMode;
        TargetWindowPolicy = settings.TargetWindowPolicy; Hotkey = settings.Hotkey;
        VoiceCommandsEnabled = settings.VoiceCommandsEnabled ?? false; DiagnosticLoggingLevel = settings.DiagnosticLoggingLevel;
        await RefreshEndpointCredentialAsync();
        LmStudioSecretStatus = await secretStore.HasSecretAsync(TranscriptionProviderIds.LmStudio, cancellationToken) ? "Configured" : "Not configured";
        OllamaSecretStatus = await secretStore.HasSecretAsync(TranscriptionProviderIds.Ollama, cancellationToken) ? "Configured" : "Not configured";
        ProviderStatus = "Not tested.";
    }

    public async Task<bool> SaveAsync(string azureKey, string lmStudioToken, string ollamaToken,
        Func<string, bool> applyHotkey, Func<bool, bool> applyVoiceCommands, CancellationToken cancellationToken)
    {
        if (!Validate() || !applyHotkey(Hotkey.Trim())) return false;
        try { await SaveConfigurationAsync(cancellationToken); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusText = $"Settings could not be saved: {exception.Message}";
            return false;
        }
        applyFailedAudioRetention(RetainFailedAudio);
        await SaveSecretAsync(ActiveEndpointCredentialId, azureKey, value => AzureSecretStatus = value, cancellationToken);
        await SaveSecretAsync(TranscriptionProviderIds.LmStudio, lmStudioToken, value => LmStudioSecretStatus = value, cancellationToken);
        await SaveSecretAsync(TranscriptionProviderIds.Ollama, ollamaToken, value => OllamaSecretStatus = value, cancellationToken);
        foreach (string credentialId in _removedCredentialIds) await secretStore.RemoveSecretAsync(credentialId, cancellationToken);
        _removedCredentialIds.Clear();
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
            await applyProvider(SelectedProviderId, cancellationToken);
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
        if (providerId == ActiveEndpointCredentialId) AzureSecretStatus = "Not configured";
        else if (providerId == TranscriptionProviderIds.LmStudio) LmStudioSecretStatus = "Not configured";
        else if (providerId == TranscriptionProviderIds.Ollama) OllamaSecretStatus = "Not configured";
        StatusText = "Protected credential removed.";
    }

    public async Task EditVocabularyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await settingsStore.EnsureVocabularyAsync(cancellationToken);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe")
            {
                UseShellExecute = true,
                Arguments = $"\"{VocabularyFilePath}\"",
            });
            StatusText = string.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.DecoderFallbackException or System.ComponentModel.Win32Exception)
        {
            StatusText = $"Vocabulary file could not be opened: {exception.Message}";
        }
    }

    private async Task SaveConfigurationAsync(CancellationToken cancellationToken)
    {
        ApplicationSettings current = await settingsStore.LoadAsync(cancellationToken);
        _changingEndpoint = true;
        try { CaptureEndpointDraft(); }
        finally { _changingEndpoint = false; OnPropertyChanged(nameof(SelectedEndpointId)); }
        await settingsStore.SaveAsync(new ApplicationSettings
        {
            TranscriptionProviderId = SelectedProviderId,
            LocalWhisperModel = "small-q5_1",
            AzureEndpoint = AzureEndpoint.Trim(),
            AzureDeployment = AzureDeployment.Trim(),
            AzureApiVersion = AzureApiVersion.Trim(),
            TranscriptionEndpoints = EndpointProfiles.Select(item => item.Profile).ToList(),
            SelectedTranscriptionEndpointId = SelectedEndpointId,
            LmStudioBaseUrl = LmStudioBaseUrl.Trim(),
            LmStudioModel = LmStudioModel.Trim(),
            OllamaBaseUrl = OllamaBaseUrl.Trim(),
            OllamaModel = OllamaModel.Trim(),
            TranscriptionLanguageMode = TranscriptionLanguageMode,
            TechnicalVocabulary = current.TechnicalVocabulary,
            RetainFailedAudio = RetainFailedAudio,
            StartWithWindows = StartWithWindows,
            ClipboardOnlyMode = ClipboardOnlyMode,
            TargetWindowPolicy = TargetWindowPolicy,
            Hotkey = Hotkey.Trim(),
            VoiceCommandsEnabled = VoiceCommandsEnabled,
            DiagnosticLoggingLevel = DiagnosticLoggingLevel,
        }, cancellationToken);
        RuntimeDiagnostics.SetLevel(DiagnosticLoggingLevel);
    }

    private async Task SaveSecretAsync(string id, string value, Action<string> update, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        await secretStore.SetSecretAsync(id, value, token); update("Configured");
    }

    private bool Validate()
    {
        if (!HotkeyGesture.TryParse(Hotkey, out _)) { StatusText = "Enter a valid hotkey such as Win+< or Ctrl+Alt+F9."; return false; }
        if (!TranscriptionLanguageModes.IsValid(TranscriptionLanguageMode)) { StatusText = "Select a valid transcription language."; return false; }
        return ValidateProvider();
    }

    private bool ValidateProvider()
    {
        StatusText = string.Empty;
        try
        {
            if (IsAzure)
            {
                CreateEndpointDraft().Validate();
            }
            if (IsLmStudio) TranscriptionProviderRegistry.ValidateServerUri(LmStudioBaseUrl, "LM Studio");
            if (IsOllama) TranscriptionProviderRegistry.ValidateServerUri(OllamaBaseUrl, "Ollama");
            return true;
        }
        catch (Exception exception) { StatusText = exception.Message; return false; }
    }

    public void AddEndpoint(bool duplicate)
    {
        if (EndpointProfiles.Count >= 32) { StatusText = "At most 32 endpoint profiles can be registered."; return; }
        TranscriptionEndpointProfile profile = duplicate ? CreateEndpointDraft() with
        {
            Id = Guid.NewGuid().ToString("N"), Name = EndpointName + " copy",
        } : new TranscriptionEndpointProfile();
        EndpointProfiles.Add(new TranscriptionEndpointItem(profile));
        SelectedEndpointId = profile.Id;
        OnPropertyChanged(nameof(CanRemoveEndpoint));
        StatusText = string.Empty;
    }

    public void RemoveEndpoint()
    {
        if (!CanRemoveEndpoint) return;
        TranscriptionEndpointItem? selected = EndpointProfiles.FirstOrDefault(profile => profile.Id == SelectedEndpointId);
        if (selected is null) return;
        _changingEndpoint = true;
        try
        {
            _removedCredentialIds.Add(selected.CredentialId);
            EndpointProfiles.Remove(selected);
            _selectedEndpointId = null;
        }
        finally { _changingEndpoint = false; }
        SelectedEndpointId = EndpointProfiles[0].Id;
        OnPropertyChanged(nameof(CanRemoveEndpoint));
    }

    public async Task ClearProgressAsync()
    {
        try
        {
            TranscriptionEndpointProfile selected = CreateEndpointDraft();
            selected.Validate();
            string directory = Path.Combine(Path.GetDirectoryName(RuntimeDiagnostics.LogPath)!, "TranscriptionJobs", selected.Id);
            if (Directory.Exists(directory))
                foreach (string path in Directory.EnumerateFiles(directory, "*.bin")) File.Delete(path);
            StatusText = "Saved transcription progress cleared for this endpoint.";
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            StatusText = $"Could not clear saved progress: {exception.Message}";
        }
        await Task.CompletedTask;
    }

    public void ShowValidationError(string message) => StatusText = message;

    private TranscriptionEndpointProfile CreateEndpointDraft() => new()
    {
        Id = SelectedEndpointId ?? TranscriptionEndpointProfile.LegacyId, Name = EndpointName.Trim(),
        Endpoint = AzureEndpoint.Trim(), Deployment = AzureDeployment.Trim(), ApiVersion = AzureApiVersion.Trim(),
        MaximumOutputTokens = MaximumOutputTokens, EstimatedTokensPerMinute = EstimatedTokensPerMinute,
        MaximumSegmentSeconds = MaximumSegmentSeconds, MaximumUploadBytes = MaximumUploadBytes,
        RequestTimeoutSeconds = RequestTimeoutSeconds, OpenAiCompatibleRoute = OpenAiCompatibleRoute,
        BearerAuthentication = BearerAuthentication, RetainProgress = RetainProgress,
    };

    private void CaptureEndpointDraft()
    {
        for (int index = 0; index < EndpointProfiles.Count; index++)
            if (EndpointProfiles[index].Id == SelectedEndpointId) { EndpointProfiles[index].Update(CreateEndpointDraft()); break; }
    }

    private async Task RefreshEndpointCredentialAsync()
    {
        string credentialId = ActiveEndpointCredentialId;
        AzureSecretStatus = "Not configured";
        try
        {
            bool exists = await secretStore.HasSecretAsync(credentialId, CancellationToken.None);
            if (credentialId == ActiveEndpointCredentialId) AzureSecretStatus = exists ? "Configured" : "Not configured";
        }
        catch (ArgumentException) { AzureSecretStatus = "Invalid profile"; }
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

public sealed record TranscriptionLanguageOption(string Id, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public sealed class TranscriptionEndpointItem(TranscriptionEndpointProfile profile) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public TranscriptionEndpointProfile Profile { get; private set; } = profile;
    public string Id => Profile.Id;
    public string Name => Profile.Name;
    public string CredentialId => Profile.CredentialId;
    public override string ToString() => Name;

    public void Update(TranscriptionEndpointProfile value)
    {
        Profile = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
    }
}
