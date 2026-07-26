using System.ComponentModel;
using System.Runtime.CompilerServices;
using VoiceType.Core;

namespace VoiceType.App;

public sealed class SettingsWindowViewModel(
    IApplicationSettingsStore settingsStore,
    ISecretStore secretStore) : INotifyPropertyChanged
{
    private string _azureEndpoint = string.Empty;
    private string _azureDeployment = string.Empty;
    private string _azureApiVersion = "preview";
    private string _technicalVocabulary = string.Empty;
    private bool _retainFailedAudio = true;
    private bool _startWithWindows;
    private bool _clipboardOnlyMode;
    private string _targetWindowPolicy = "OriginalTarget";
    private string _diagnosticLoggingLevel = "Information";
    private string _keyStatusText = "Not configured";
    private string _statusText = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string AzureEndpoint
    {
        get => _azureEndpoint;
        set => SetProperty(ref _azureEndpoint, value);
    }

    public string AzureDeployment
    {
        get => _azureDeployment;
        set => SetProperty(ref _azureDeployment, value);
    }

    public string AzureApiVersion
    {
        get => _azureApiVersion;
        set => SetProperty(ref _azureApiVersion, value);
    }

    public string TechnicalVocabulary
    {
        get => _technicalVocabulary;
        set => SetProperty(ref _technicalVocabulary, value);
    }

    public bool RetainFailedAudio
    {
        get => _retainFailedAudio;
        set => SetProperty(ref _retainFailedAudio, value);
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetProperty(ref _startWithWindows, value);
    }

    public bool ClipboardOnlyMode
    {
        get => _clipboardOnlyMode;
        set => SetProperty(ref _clipboardOnlyMode, value);
    }

    public string TargetWindowPolicy
    {
        get => _targetWindowPolicy;
        set => SetProperty(ref _targetWindowPolicy, value);
    }

    public string DiagnosticLoggingLevel
    {
        get => _diagnosticLoggingLevel;
        set => SetProperty(ref _diagnosticLoggingLevel, value);
    }

    public string KeyStatusText
    {
        get => _keyStatusText;
        private set => SetProperty(ref _keyStatusText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        ApplicationSettings settings = await settingsStore.LoadAsync(cancellationToken);
        AzureEndpoint = settings.AzureEndpoint;
        AzureDeployment = settings.AzureDeployment;
        AzureApiVersion = settings.AzureApiVersion;
        TechnicalVocabulary = settings.TechnicalVocabulary;
        RetainFailedAudio = settings.RetainFailedAudio;
        StartWithWindows = settings.StartWithWindows;
        ClipboardOnlyMode = settings.ClipboardOnlyMode;
        TargetWindowPolicy = settings.TargetWindowPolicy;
        DiagnosticLoggingLevel = settings.DiagnosticLoggingLevel;
        KeyStatusText = await secretStore.HasSecretAsync(cancellationToken)
            ? "Configured"
            : "Not configured";
    }

    public async Task<bool> SaveAsync(string replacementKey, CancellationToken cancellationToken)
    {
        if (!Validate())
        {
            return false;
        }

        ApplicationSettings settings = new()
        {
            AzureEndpoint = AzureEndpoint.Trim(),
            AzureDeployment = AzureDeployment.Trim(),
            AzureApiVersion = AzureApiVersion.Trim(),
            TechnicalVocabulary = TechnicalVocabulary,
            RetainFailedAudio = RetainFailedAudio,
            StartWithWindows = StartWithWindows,
            ClipboardOnlyMode = ClipboardOnlyMode,
            TargetWindowPolicy = TargetWindowPolicy,
            DiagnosticLoggingLevel = DiagnosticLoggingLevel,
        };
        await settingsStore.SaveAsync(settings, cancellationToken);
        if (!string.IsNullOrWhiteSpace(replacementKey))
        {
            await secretStore.SetSecretAsync(replacementKey, cancellationToken);
            KeyStatusText = "Configured";
        }

        StatusText = "Settings saved. Restart the app to apply Azure changes.";
        return true;
    }

    public async Task RemoveKeyAsync(CancellationToken cancellationToken)
    {
        await secretStore.RemoveSecretAsync(cancellationToken);
        KeyStatusText = "Not configured";
        StatusText = "API key removed.";
    }

    private bool Validate()
    {
        if (string.IsNullOrWhiteSpace(AzureEndpoint) && string.IsNullOrWhiteSpace(AzureDeployment))
        {
            StatusText = string.Empty;
            return true;
        }

        if (!Uri.TryCreate(AzureEndpoint, UriKind.Absolute, out Uri? endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttps)
        {
            StatusText = "The Azure endpoint must be a valid HTTPS address.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(AzureDeployment))
        {
            StatusText = "A deployment name is required when an endpoint is provided.";
            return false;
        }

        StatusText = string.Empty;
        return true;
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
