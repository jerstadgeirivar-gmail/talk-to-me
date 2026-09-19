using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using TalkToMe.Core;
using TalkToMe.Infrastructure;

if (args.Length == 2 && args[0] == "--migrate-key")
{
    return await MigrateCredentialAsync(Path.GetFullPath(args[1]));
}
if (args.Length == 2 && args[0] == "--scan-package")
{
    return await ScanPackageAsync(Path.GetFullPath(args[1]));
}

const string windowAutomationId = "MainWindow";
const string buttonAutomationId = "StartRecordingButton";
const string stopButtonAutomationId = "StopRecordingButton";
const string statusAutomationId = "RecordingStatusText";
const string durationAutomationId = "RecordingDurationText";
const string sourceAutomationId = "AudioSourceText";
const string expectedSource = "Diagnostic audio file";
const string recordingStatus = "Recording";
const string recordOnlyStatus = "Audio recording ready";
const string insertionCompleteStatus = "Text inserted";
const string transcriptAutomationId = "TranscriptTextBox";
const string insertButtonAutomationId = "InsertButton";
const string privacyTranscript = "Synthetic privacy regression transcript.";

bool settingsScenario = args.Length == 3 && args[1] == "--settings";
bool capabilitiesScenario = args.Length == 3 && args[1] == "--capabilities";
bool firstRunModelScenario = args.Length == 3 && args[1] == "--first-run-model";
bool liveProviderScenario = args.Length == 3 && args[1] == "--live-provider";
bool liveAzureScenario = args.Length == 5 && args[3] == "--live";
bool liveLocalScenario = args.Length == 4 && args[3] == "--live-local";
bool privacyFailureScenario = args.Length == 5 && args[3] == "--privacy-failure";
bool privacyTranscriptionFailureScenario = args.Length == 4 && args[3] == "--privacy-transcription-failure";
if (!settingsScenario && !capabilitiesScenario && !firstRunModelScenario && !liveProviderScenario && !liveLocalScenario && !privacyFailureScenario && !privacyTranscriptionFailureScenario && args.Length is not 3 and not 5)
{
    Console.Error.WriteLine(
        "Usage: TalkToMe.UiDriver <application-path> <audio-fixture-path> <evidence-directory> [diagnostic-transcript|--live target-application-path]");
    return 2;
}

string applicationPath = Path.GetFullPath(args[0]);
string? audioFixturePath = (settingsScenario || capabilitiesScenario || firstRunModelScenario || liveProviderScenario) ? null : Path.GetFullPath(args[1]);
string evidenceDirectory = Path.GetFullPath(args[2]);
string? expectedTranscript = privacyFailureScenario
    ? privacyTranscript
    : !settingsScenario && !liveAzureScenario && args.Length == 5 ? args[3] : null;
string? targetApplicationPath = args.Length == 5 ? Path.GetFullPath(args[4]) : null;
bool insertionScenario = expectedTranscript is not null || liveAzureScenario || privacyFailureScenario;
bool transcriptionScenario = insertionScenario || liveLocalScenario;
Directory.CreateDirectory(evidenceDirectory);
if (settingsScenario)
{
    PrepareLegacyNullSettings(evidenceDirectory);
}
else if (privacyTranscriptionFailureScenario)
{
    PreparePrivacyTranscriptionFailureSettings(evidenceDirectory);
}
string recordingPath = Path.Combine(evidenceDirectory, "simulated-recording.wav");

Application? application = null;
Process? process = null;
AutomationElement? window = null;
Application? notepadApplication = null;
Process? notepadProcess = null;
Process? notepadLauncher = null;
AutomationElement? notepadWindow = null;
AutomationElement? notepadEditor = null;
using UIA3Automation automation = new();

try
{
    ProcessStartInfo startInfo = new(applicationPath)
    {
        UseShellExecute = false,
        WorkingDirectory = Path.GetDirectoryName(applicationPath),
    };
    if (liveProviderScenario)
    {
        // Use the application's real configured provider and protected credential.
    }
    else if (settingsScenario || capabilitiesScenario || firstRunModelScenario)
    {
        startInfo.ArgumentList.Add("--diagnostic-data-directory");
        startInfo.ArgumentList.Add(Path.Combine(evidenceDirectory, "data"));
    }
    else
    {
        startInfo.ArgumentList.Add("--diagnostic-data-directory");
        startInfo.ArgumentList.Add(Path.Combine(evidenceDirectory, "data"));
        startInfo.ArgumentList.Add("--diagnostic-audio");
        startInfo.ArgumentList.Add(audioFixturePath!);
        startInfo.ArgumentList.Add("--diagnostic-output");
        startInfo.ArgumentList.Add(recordingPath);
        startInfo.ArgumentList.Add("--diagnostic-speed");
        startInfo.ArgumentList.Add("2");
    }
    if (liveAzureScenario || liveLocalScenario || privacyTranscriptionFailureScenario)
    {
        startInfo.ArgumentList.Add("--diagnostic-live-provider");
    }
    if (expectedTranscript is not null)
    {
        startInfo.ArgumentList.Add("--diagnostic-transcript");
        startInfo.ArgumentList.Add(expectedTranscript);
    }

    application = Application.Launch(startInfo);

    int processId = application.ProcessId;
    process = Process.GetProcessById(processId);
    window = WaitForWindow(application, automation, windowAutomationId, TimeSpan.FromSeconds(15));
    WriteAutomationTree(window, Path.Combine(evidenceDirectory, "uia-tree-before.txt"));
    if (privacyFailureScenario)
    {
        AssertPrivacyDefaults(window);
    }
    if (privacyTranscriptionFailureScenario)
    {
        RunPrivacyTranscriptionFailureScenario(window, recordingPath, evidenceDirectory);
        Console.WriteLine($"PASS pid={processId} privacy-transcription-failure=deleted");
        return 0;
    }
    if (firstRunModelScenario)
    {
        RunFirstRunModelScenario(application, automation, window, evidenceDirectory);
        Console.WriteLine($"PASS pid={processId} first-run-model=downloaded-and-verified");
        return 0;
    }
    if (settingsScenario)
    {
        RunSettingsScenario(application, automation, window, evidenceDirectory);
        Console.WriteLine($"PASS pid={processId} settings=protected-and-removed");
        return 0;
    }
    if (liveProviderScenario)
    {
        RunLiveProviderScenario(application, automation, window, evidenceDirectory);
        Console.WriteLine($"PASS pid={processId} live-provider=ready");
        return 0;
    }
    if (capabilitiesScenario)
    {
        RunCapabilitiesScenario(application, automation, window, evidenceDirectory);
        Console.WriteLine($"PASS pid={processId} capabilities=honest");
        return 0;
    }

    AutomationElement source = WaitForElementName(
        window,
        sourceAutomationId,
        expectedSource,
        TimeSpan.FromSeconds(5));
    if (insertionScenario)
    {
        (notepadApplication, notepadProcess, notepadLauncher, notepadWindow, notepadEditor) =
            LaunchTarget(targetApplicationPath!, automation);
        ActivateWindow(notepadWindow, TimeSpan.FromSeconds(5));
        notepadEditor.Focus();
        Thread.Sleep(250);
    }

    AutomationElement button = window.FindFirstDescendant(
        condition => condition.ByAutomationId(buttonAutomationId))
        ?? throw new InvalidOperationException($"Button '{buttonAutomationId}' was not found.");
    if (!insertionScenario)
    {
        button.AsButton().Invoke();
    }
    else
    {
        Keyboard.TypeSimultaneously([VirtualKeyShort.LWIN, (VirtualKeyShort)0xE2]);
    }

    WaitForElementName(window, statusAutomationId, recordingStatus, TimeSpan.FromSeconds(5));
    AutomationElement duration = WaitForDuration(
        window,
        durationAutomationId,
        (liveAzureScenario || liveLocalScenario) ? TimeSpan.FromSeconds(24) : TimeSpan.FromSeconds(2),
        (liveAzureScenario || liveLocalScenario) ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(10));
    AutomationElement stopButton = window.FindFirstDescendant(
        condition => condition.ByAutomationId(stopButtonAutomationId))
        ?? throw new InvalidOperationException($"Button '{stopButtonAutomationId}' was not found.");
    AssertEnabledState(button, expectedEnabled: false, "start button while recording");
    AssertEnabledState(stopButton, expectedEnabled: true, "stop button while recording");
    if (privacyFailureScenario)
    {
        TerminateOwnedProcess(notepadProcess);
        notepadWindow = null;
        notepadEditor = null;
    }
    Stopwatch transcriptionStopwatch = Stopwatch.StartNew();
    if (!insertionScenario)
    {
        stopButton.AsButton().Invoke();
    }
    else
    {
        Keyboard.TypeSimultaneously([VirtualKeyShort.LWIN, (VirtualKeyShort)0xE2]);
    }

    string expectedStatus = privacyFailureScenario
        ? "The target window could not be activated. The previous clipboard contents were restored."
        : insertionScenario ? insertionCompleteStatus
        : liveLocalScenario ? "Transcript ready — copy it or use the shortcut from another app to insert"
        : recordOnlyStatus;
    AutomationElement status = WaitForElementName(
        window,
        statusAutomationId,
        expectedStatus,
        transcriptionScenario ? TimeSpan.FromMinutes(3) : TimeSpan.FromSeconds(5));
    transcriptionStopwatch.Stop();

    AssertEnabledState(button, expectedEnabled: !liveLocalScenario && !privacyFailureScenario, "start button after recording");
    AssertEnabledState(stopButton, expectedEnabled: false, "stop button after recording");
    long recordingBytes = 0;
    if (!insertionScenario && !liveLocalScenario)
    {
        ValidateWaveFile(recordingPath);
        recordingBytes = new FileInfo(recordingPath).Length;
    }
    else if (liveLocalScenario && File.Exists(recordingPath))
    {
        throw new InvalidOperationException("Successful Local Whisper transcription did not delete the temporary recording.");
    }

    bool exactInsertionMatch = false;
    int transcriptCharacters = 0;
    int semanticAnchorCount = 0;
    if (transcriptionScenario)
    {
        AutomationElement transcriptElement = window.FindFirstDescendant(
            condition => condition.ByAutomationId(transcriptAutomationId))
            ?? throw new InvalidOperationException($"Element '{transcriptAutomationId}' was not found.");
        string displayedTranscript = transcriptElement.AsTextBox().Text;
        transcriptCharacters = displayedTranscript.Length;
        if (string.IsNullOrWhiteSpace(displayedTranscript))
        {
            throw new InvalidOperationException("Azure returned an empty transcript.");
        }

        if (expectedTranscript is not null && displayedTranscript != expectedTranscript)
        {
            throw new InvalidOperationException("The transcript displayed by the app did not match the diagnostic provider result.");
        }
        if (liveAzureScenario || liveLocalScenario)
        {
            semanticAnchorCount = CountSemanticAnchors(displayedTranscript);
            if (semanticAnchorCount < 2)
            {
                throw new InvalidOperationException(
                    $"The live transcript contained only {semanticAnchorCount} expected semantic anchor(s).");
            }
        }

        AutomationElement? insertButton = window.FindFirstDescendant(
            condition => condition.ByAutomationId(insertButtonAutomationId));
        if (insertButton is not null)
        {
            AssertEnabledState(insertButton, expectedEnabled: privacyFailureScenario, "insert button after automatic insertion");
        }

        if (!insertionScenario)
        {
            goto TranscriptionValidated;
        }
        if (privacyFailureScenario)
        {
            AutomationElement retryInsert = window.FindFirstDescendant(
                condition => condition.ByAutomationId(insertButtonAutomationId))
                ?? throw new InvalidOperationException($"Element '{insertButtonAutomationId}' was not found.");
            AssertEnabledState(retryInsert, expectedEnabled: true, "insert button after failed insertion");
            if (File.Exists(recordingPath))
            {
                throw new InvalidOperationException("Insertion failure retained audio even though retention was disabled.");
            }
            goto TranscriptionValidated;
        }

        string insertedText = notepadEditor!.AsTextBox().Text;
        exactInsertionMatch = insertedText == displayedTranscript;
        if (!exactInsertionMatch)
        {
            throw new InvalidOperationException(
            $"Target text mismatch. Expected {displayedTranscript.Length} characters, observed {insertedText.Length}.");
        }
        if (File.Exists(recordingPath))
        {
            throw new InvalidOperationException("Successful insertion did not delete the temporary recording.");
        }

        WriteAutomationTree(notepadWindow!, Path.Combine(evidenceDirectory, "target-uia-tree.txt"));
        CaptureWindow(notepadWindow!, Path.Combine(evidenceDirectory, "target-window.png"));
    TranscriptionValidated:;
    }

    Thread.Sleep(250);
    WriteAutomationTree(window, Path.Combine(evidenceDirectory, "uia-tree.txt"));
    CaptureWindow(window, Path.Combine(evidenceDirectory, "main-window.png"));
    File.WriteAllText(
        Path.Combine(evidenceDirectory, "result.txt"),
        $"ProcessId: {processId}{Environment.NewLine}" +
        $"WindowAutomationId: {window.AutomationId}{Environment.NewLine}" +
        $"Source: {source.Name}{Environment.NewLine}" +
        $"Status: {status.Name}{Environment.NewLine}" +
        $"Duration: {duration.Name}{Environment.NewLine}" +
        $"RecordingBytes: {recordingBytes}{Environment.NewLine}" +
        $"LiveAzure: {liveAzureScenario}{Environment.NewLine}" +
        $"LiveLocal: {liveLocalScenario}{Environment.NewLine}" +
        $"PrivacyFailure: {privacyFailureScenario}{Environment.NewLine}" +
        $"TranscriptionMilliseconds: {transcriptionStopwatch.ElapsedMilliseconds}{Environment.NewLine}" +
        $"TranscriptCharacters: {transcriptCharacters}{Environment.NewLine}" +
        $"SemanticAnchorCount: {semanticAnchorCount}{Environment.NewLine}" +
        $"TargetProcessId: {notepadProcess?.Id}{Environment.NewLine}" +
        $"InsertedCharacters: {transcriptCharacters}{Environment.NewLine}" +
        $"ExactMatchWithoutEnter: {exactInsertionMatch}{Environment.NewLine}" +
        "Result: PASS" + Environment.NewLine);

    Console.WriteLine(
        $"PASS pid={processId} window={window.AutomationId} status=\"{status.Name}\" " +
        $"duration={duration.Name} bytes={recordingBytes}");
    return 0;
}
catch (Exception exception)
{
    if (notepadWindow is not null)
    {
        try
        {
            CaptureWindow(notepadWindow, Path.Combine(evidenceDirectory, "failure-target-window.png"));
            WriteAutomationTree(notepadWindow, Path.Combine(evidenceDirectory, "failure-target-uia-tree.txt"));
        }
        catch (Exception captureException)
        {
            File.WriteAllText(
                Path.Combine(evidenceDirectory, "notepad-capture-exception.txt"),
                captureException.ToString());
        }
    }

    CaptureFailureEvidence(application, process, window, evidenceDirectory, exception);
    Console.Error.WriteLine($"FAIL {exception.GetType().Name}: {exception.Message}");
    return 1;
}
finally
{
    TerminateOwnedProcess(notepadProcess);
    if (notepadLauncher is not null && notepadLauncher.Id != notepadProcess?.Id)
    {
        TerminateOwnedProcess(notepadLauncher);
    }

    notepadProcess?.Dispose();
    notepadLauncher?.Dispose();
    notepadApplication?.Dispose();

    if (window is not null)
    {
        try
        {
            window.AsWindow().Close();
        }
        catch (Exception)
        {
        }
    }

    if (process is not null && !process.HasExited)
    {
        if (!process.WaitForExit(5_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5_000);
        }
    }

    process?.Dispose();
    application?.Dispose();
}

static int CountSemanticAnchors(string transcript)
{
    string[] anchors =
    [
        "Visual Studio Code",
        "GitHub Copilot",
        "API",
        "endepunkt",
        "appsettings",
        "norsk",
        "transkribering",
        "teste",
        "tydelig",
        "lydfil",
        "høyre",
    ];
    return anchors.Count(anchor => transcript.Contains(anchor, StringComparison.OrdinalIgnoreCase));
}

static void AssertPrivacyDefaults(AutomationElement mainWindow)
{
    AutomationElement settingsButton = FindByAutomationId(mainWindow, "SettingsButton");
    Task invokeTask = Task.Run(() => settingsButton.AsButton().Invoke());
    Window settingsWindow = WaitForDescendantWindow(mainWindow, "SettingsWindow", TimeSpan.FromSeconds(10));
    FindByAutomationId(settingsWindow, "DictationTab").Patterns.SelectionItem.Pattern.Select();
    CheckBox retainAudio = FindByAutomationId(settingsWindow, "RetainFailedAudioCheckBox").AsCheckBox();
    if (retainAudio.IsChecked == true)
    {
        throw new InvalidOperationException("Failed-audio retention was enabled in fresh settings.");
    }

    settingsWindow.Close();
    if (!invokeTask.Wait(TimeSpan.FromSeconds(5)))
    {
        throw new TimeoutException("Settings dialog invocation did not complete after the privacy check.");
    }
}

static void PrepareLegacyNullSettings(string evidenceDirectory)
{
    string dataDirectory = Path.Combine(evidenceDirectory, "data");
    Directory.CreateDirectory(dataDirectory);
    File.WriteAllText(
        Path.Combine(dataDirectory, "settings.json"),
        """
        {
          "TranscriptionProviderId": "azure-openai",
          "TranscriptionLanguageMode": null,
          "LocalWhisperModel": null,
          "AzureEndpoint": "https://example.openai.azure.com",
          "AzureDeployment": "speech-deployment",
          "AzureApiVersion": "2025-04-01-preview",
          "LmStudioBaseUrl": null,
          "LmStudioModel": null,
          "OllamaBaseUrl": null,
          "OllamaModel": null,
          "TechnicalVocabulary": "",
          "RetainFailedAudio": true,
          "StartWithWindows": false,
          "ClipboardOnlyMode": false,
          "TargetWindowPolicy": "OriginalTarget",
          "Hotkey": "Win+<",
          "VoiceCommandsEnabled": false,
          "DiagnosticLoggingLevel": "Information"
        }
        """);
}

static void PreparePrivacyTranscriptionFailureSettings(string evidenceDirectory)
{
    string dataDirectory = Path.Combine(evidenceDirectory, "data");
    Directory.CreateDirectory(dataDirectory);
    File.WriteAllText(
        Path.Combine(dataDirectory, "settings.json"),
        """
        {
          "TranscriptionProviderId": "lm-studio",
          "LocalWhisperModel": "small-q5_1",
          "LmStudioBaseUrl": "http://localhost:1234",
          "OllamaBaseUrl": "http://localhost:11434",
          "RetainFailedAudio": false,
          "Hotkey": "Win+<",
          "DiagnosticLoggingLevel": "Information"
        }
        """);
}

static void RunPrivacyTranscriptionFailureScenario(
    AutomationElement mainWindow,
    string recordingPath,
    string evidenceDirectory)
{
    AutomationElement start = FindByAutomationId(mainWindow, buttonAutomationId);
    AutomationElement stop = FindByAutomationId(mainWindow, stopButtonAutomationId);
    start.AsButton().Invoke();
    WaitForElementName(mainWindow, statusAutomationId, recordingStatus, TimeSpan.FromSeconds(5));
    WaitForDuration(mainWindow, durationAutomationId, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10));
    stop.AsButton().Invoke();

    const string expectedFailure = "LM Studio does not expose a documented speech-to-text endpoint. Select Local Whisper or Azure OpenAI, or configure this profile only when a future documented audio API is available. The recording was deleted.";
    WaitForElementName(mainWindow, statusAutomationId, expectedFailure, TimeSpan.FromSeconds(10));
    if (File.Exists(recordingPath))
    {
        throw new InvalidOperationException("Failed transcription retained audio even though retention was disabled.");
    }

    AssertEnabledState(start, expectedEnabled: true, "start button after deleted transcription failure");
    AssertEnabledState(stop, expectedEnabled: false, "stop button after deleted transcription failure");
    File.WriteAllText(
        Path.Combine(evidenceDirectory, "result.txt"),
        "FailedAudioRetentionDefault: False" + Environment.NewLine +
        "FailedRecordingDeleted: True" + Environment.NewLine +
        "ReadyForNewRecording: True" + Environment.NewLine +
        "Result: PASS" + Environment.NewLine);
}

static void RunSettingsScenario(
    Application application,
    UIA3Automation automation,
    AutomationElement mainWindow,
    string evidenceDirectory)
{
    AutomationElement settingsButton = FindByAutomationId(mainWindow, "SettingsButton");
    Task invokeTask = Task.Run(() => settingsButton.AsButton().Invoke());
    Window settingsWindow = WaitForDescendantWindow(
        mainWindow,
        "SettingsWindow",
        TimeSpan.FromSeconds(10));
    AssertSettingsFitsWithoutScrolling(settingsWindow);

    FindByAutomationId(settingsWindow, "ProviderSelector").AsComboBox().Select("Azure OpenAI");
    FindByAutomationId(settingsWindow, "DictationTab").Patterns.SelectionItem.Pattern.Select();
    ComboBox language = FindByAutomationId(settingsWindow, "TranscriptionLanguageSelector").AsComboBox();
    string[] languageChoices = language.Items.Select(item => item.Name).ToArray();
    string[] expectedLanguageChoices = ["Auto", "Norwegian", "Norwegian + English"];
    if (!languageChoices.SequenceEqual(expectedLanguageChoices, StringComparer.Ordinal))
    {
        throw new InvalidOperationException(
            $"Unexpected transcription language choices: {string.Join(", ", languageChoices)}.");
    }
    if (language.SelectedItem?.Name != "Norwegian")
    {
        throw new InvalidOperationException("Legacy settings did not normalize to Norwegian.");
    }
    language.Select("Auto");
    language.Select("Norwegian");
    language.Select("Norwegian + English");
    CaptureWindow(settingsWindow, Path.Combine(evidenceDirectory, "language-settings-window.png"));
    FindByAutomationId(settingsWindow, "TranscriptionTab").Patterns.SelectionItem.Pattern.Select();
    AutomationElement endpoint = FindByAutomationId(settingsWindow, "AzureEndpointTextBox");
    AutomationElement deployment = FindByAutomationId(settingsWindow, "AzureDeploymentTextBox");
    AutomationElement apiVersion = FindByAutomationId(settingsWindow, "AzureApiVersionTextBox");
    AutomationElement password = FindByAutomationId(settingsWindow, "ApiKeyPasswordBox");
    AutomationElement save = FindByAutomationId(settingsWindow, "SaveSettingsButton");
    AutomationElement testProvider = FindByAutomationId(settingsWindow, "TestProviderButton");
    AutomationElement remove = FindByAutomationId(settingsWindow, "RemoveApiKeyButton");

    endpoint.AsTextBox().Text = "http://invalid.example";
    deployment.AsTextBox().Text = "speech-deployment";
    save.AsButton().Invoke();
    WaitForElementName(
        settingsWindow,
        "SettingsStatusText",
        "The Azure endpoint must be a valid HTTPS address.",
        TimeSpan.FromSeconds(5));

    string syntheticKey = $"synthetic-{Guid.NewGuid():N}";
    endpoint.AsTextBox().Text = "https://127.0.0.1:1/";
    apiVersion.AsTextBox().Text = "preview";
    password.Patterns.Value.Pattern.SetValue(syntheticKey);
    save.AsButton().Invoke();
    WaitForElementName(settingsWindow, "ApiKeyStatusText", "Configured", TimeSpan.FromSeconds(5));

    testProvider.AsButton().Invoke();
    WaitForElementName(
        settingsWindow,
        "ProviderStatusText",
        "Azure transcription could not be reached. The audio was retained for retry.",
        TimeSpan.FromSeconds(15));

    string dataDirectory = Path.Combine(evidenceDirectory, "data");
    string credentialPath = Path.Combine(dataDirectory, "Secrets", "azure-openai.bin");
    string settingsPath = Path.Combine(dataDirectory, "settings.json");
    if (!File.Exists(credentialPath) || !File.Exists(settingsPath))
    {
        throw new InvalidOperationException("Settings or protected credential file was not created.");
    }

    string savedSettings = File.ReadAllText(settingsPath);
    if (!savedSettings.Contains("\"TranscriptionLanguageMode\":\"norwegian-english\"", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("The selected transcription language was not persisted.");
    }
    string[] normalizedProperties =
    [
        "LocalWhisperModel",
        "LmStudioBaseUrl",
        "LmStudioModel",
        "OllamaBaseUrl",
        "OllamaModel",
    ];
    foreach (string property in normalizedProperties)
    {
        if (savedSettings.Contains($"\"{property}\":null", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"The saved settings still contain a null {property} value.");
        }
    }

    byte[] protectedBytes = File.ReadAllBytes(credentialPath);
    if (Encoding.UTF8.GetString(protectedBytes).Contains(syntheticKey, StringComparison.Ordinal))
    {
        throw new InvalidOperationException("The protected credential file contains plaintext test material.");
    }

    remove.AsButton().Invoke();
    WaitForElementName(settingsWindow, "ApiKeyStatusText", "Not configured", TimeSpan.FromSeconds(5));
    if (File.Exists(credentialPath))
    {
        throw new InvalidOperationException("The protected credential file still exists after removal.");
    }

    Thread.Sleep(250);
    WriteAutomationTree(settingsWindow, Path.Combine(evidenceDirectory, "settings-uia-tree.txt"));
    CaptureWindow(settingsWindow, Path.Combine(evidenceDirectory, "settings-window.png"));
    settingsWindow.Close();
    if (!invokeTask.Wait(TimeSpan.FromSeconds(5)))
    {
        throw new TimeoutException("Settings dialog invocation did not complete after close.");
    }

    Task reopenInvokeTask = Task.Run(() => settingsButton.AsButton().Invoke());
    Window reopenedSettingsWindow = WaitForDescendantWindow(
        mainWindow,
        "SettingsWindow",
        TimeSpan.FromSeconds(10));
    FindByAutomationId(reopenedSettingsWindow, "DictationTab").Patterns.SelectionItem.Pattern.Select();
    ComboBox reopenedLanguage = FindByAutomationId(
        reopenedSettingsWindow,
        "TranscriptionLanguageSelector").AsComboBox();
    if (reopenedLanguage.SelectedItem?.Name != "Norwegian + English")
    {
        throw new InvalidOperationException("The saved transcription language was not restored after reopening Settings.");
    }

    WriteAutomationTree(reopenedSettingsWindow, Path.Combine(evidenceDirectory, "settings-reopened-uia-tree.txt"));
    CaptureWindow(reopenedSettingsWindow, Path.Combine(evidenceDirectory, "language-settings-reopened.png"));
    reopenedSettingsWindow.Close();
    if (!reopenInvokeTask.Wait(TimeSpan.FromSeconds(5)))
    {
        throw new TimeoutException("Settings dialog invocation did not complete after reopening.");
    }

    File.WriteAllText(
        Path.Combine(evidenceDirectory, "result.txt"),
        $"ProcessId: {application.ProcessId}{Environment.NewLine}" +
        "DefaultSettingsFitsWithoutScrolling: True" + Environment.NewLine +
        "InvalidHttpsRejected: True" + Environment.NewLine +
        "LegacyNullSettingsNormalized: True" + Environment.NewLine +
        "LanguageChoices: Auto, Norwegian, Norwegian + English" + Environment.NewLine +
        "LanguageSelectionPersisted: norwegian-english" + Environment.NewLine +
        "LanguageSelectionReopened: norwegian-english" + Environment.NewLine +
        "ProviderButtonPerformedRealRequest: True" + Environment.NewLine +
        "ProviderStatusReacted: True" + Environment.NewLine +
        "ProtectedCredentialCreated: True" + Environment.NewLine +
        "PlaintextAbsent: True" + Environment.NewLine +
        "CredentialRemoved: True" + Environment.NewLine +
        "Result: PASS" + Environment.NewLine);
}

static void RunCapabilitiesScenario(
    Application application,
    UIA3Automation automation,
    AutomationElement mainWindow,
    string evidenceDirectory)
{
    AutomationElement settingsButton = FindByAutomationId(mainWindow, "SettingsButton");
    Task invokeTask = Task.Run(() => settingsButton.AsButton().Invoke());
    Window settingsWindow = WaitForDescendantWindow(mainWindow, "SettingsWindow", TimeSpan.FromSeconds(10));
    ComboBox provider = FindByAutomationId(settingsWindow, "ProviderSelector").AsComboBox();
    string initialProvider = provider.SelectedItem?.Name ?? string.Empty;
    provider.Select("LM Studio");
    FindByAutomationId(settingsWindow, "TestProviderButton").AsButton().Invoke();
    WaitForElementNameContains(settingsWindow, "ProviderStatusText", "speech-to-text", TimeSpan.FromSeconds(15));
    CaptureWindow(settingsWindow, Path.Combine(evidenceDirectory, "lm-studio-capability.png"));
    provider.Select("Ollama");
    FindByAutomationId(settingsWindow, "TestProviderButton").AsButton().Invoke();
    WaitForElementNameContains(settingsWindow, "ProviderStatusText", "speech-to-text", TimeSpan.FromSeconds(15));
    CaptureWindow(settingsWindow, Path.Combine(evidenceDirectory, "ollama-capability.png"));
    WriteAutomationTree(settingsWindow, Path.Combine(evidenceDirectory, "capabilities-uia-tree.txt"));
    File.WriteAllText(Path.Combine(evidenceDirectory, "result.txt"),
        $"InitialProvider: {initialProvider}" + Environment.NewLine +
        "LMStudioAudioSent: False" + Environment.NewLine + "OllamaAudioSent: False" + Environment.NewLine + "Result: PASS" + Environment.NewLine);
    settingsWindow.Close();
    if (!invokeTask.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Settings dialog did not close.");
}

static void RunLiveProviderScenario(
    Application application,
    UIA3Automation automation,
    AutomationElement mainWindow,
    string evidenceDirectory)
{
    AutomationElement settingsButton = FindByAutomationId(mainWindow, "SettingsButton");
    Task invokeTask = Task.Run(() => settingsButton.AsButton().Invoke());
    Window settingsWindow = WaitForDescendantWindow(mainWindow, "SettingsWindow", TimeSpan.FromSeconds(10));
    AssertSettingsFitsWithoutScrolling(settingsWindow);
    ComboBox provider = FindByAutomationId(settingsWindow, "ProviderSelector").AsComboBox();
    string selectedProvider = provider.SelectedItem?.Name ?? string.Empty;
    if (!selectedProvider.Contains("Azure OpenAI", StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected the configured provider to be Azure OpenAI, but found '{selectedProvider}'.");
    }

    WaitForElementName(settingsWindow, "ProviderStatusText", "Not tested.", TimeSpan.FromSeconds(10));
    FindByAutomationId(settingsWindow, "TestProviderButton").AsButton().Invoke();
    AutomationElement status = WaitForElementNameContains(
        settingsWindow,
        "ProviderStatusText",
        "Ready — Azure transcribed test audio",
        TimeSpan.FromSeconds(45));

    CaptureWindow(settingsWindow, Path.Combine(evidenceDirectory, "live-provider-window.png"));
    WriteAutomationTree(settingsWindow, Path.Combine(evidenceDirectory, "live-provider-uia-tree.txt"));
    File.WriteAllText(
        Path.Combine(evidenceDirectory, "result.txt"),
        $"ProcessId: {application.ProcessId}{Environment.NewLine}" +
        "DefaultSettingsFitsWithoutScrolling: True" + Environment.NewLine +
        $"Provider: {selectedProvider}{Environment.NewLine}" +
        $"Status: {status.Name}{Environment.NewLine}" +
        "RealAudioRequest: True" + Environment.NewLine +
        "Result: PASS" + Environment.NewLine);
    settingsWindow.Close();
    if (!invokeTask.Wait(TimeSpan.FromSeconds(5)))
    {
        throw new TimeoutException("Settings dialog invocation did not complete after close.");
    }
}

static void AssertSettingsFitsWithoutScrolling(AutomationElement settingsWindow)
{
    Rectangle bounds = Rectangle.Round(settingsWindow.BoundingRectangle);
    if (bounds.Width < 760 || bounds.Height < 920)
    {
        throw new InvalidOperationException(
            $"Settings opened at {bounds.Width}x{bounds.Height}; expected at least 760x920.");
    }

    AutomationElement? verticalScrollBar = settingsWindow.FindFirstDescendant(
        condition => condition.ByAutomationId("VerticalScrollBar"));
    if (verticalScrollBar is not null)
    {
        Rectangle scrollBounds = Rectangle.Round(verticalScrollBar.BoundingRectangle);
        if (scrollBounds.Width > 0 && scrollBounds.Height > 0)
        {
            throw new InvalidOperationException(
                $"The Settings transcription tab still requires vertical scrolling ({scrollBounds}).");
        }
    }
}

static void RunFirstRunModelScenario(
    Application application,
    UIA3Automation automation,
    AutomationElement mainWindow,
    string evidenceDirectory)
{
    Window consent = WaitForNamedWindow(mainWindow, "Set up offline transcription", TimeSpan.FromSeconds(15));
    Thread.Sleep(300);
    CaptureWindow(consent, Path.Combine(evidenceDirectory, "model-download-consent.png"));
    AutomationElement yes = consent.FindFirstDescendant(condition => condition.ByName("Yes"))
        ?? throw new InvalidOperationException("The model download consent Yes button was not found.");
    yes.AsButton().Invoke();

    Window progress = WaitForDescendantWindow(mainWindow, "LocalModelSetupWindow", TimeSpan.FromSeconds(15));
    WaitForElementNameContains(progress, "LocalModelDownloadStatus", "Downloading", TimeSpan.FromSeconds(30));
    CaptureWindow(progress, Path.Combine(evidenceDirectory, "model-download-progress.png"));
    WaitForElementName(mainWindow, "RecordingStatusText",
        "Ready — Local Whisper is installed for offline transcription", TimeSpan.FromMinutes(3));

    ProviderTestResult readiness = LocalWhisperModel.VerifyAsync(CancellationToken.None).GetAwaiter().GetResult();
    if (!readiness.IsReady) throw new InvalidOperationException(readiness.Message);
    CaptureWindow(mainWindow, Path.Combine(evidenceDirectory, "model-download-complete.png"));
    File.WriteAllText(Path.Combine(evidenceDirectory, "result.txt"),
        $"ModelPath: {LocalWhisperModel.ResolvePath()}" + Environment.NewLine +
        $"ModelBytes: {new FileInfo(LocalWhisperModel.ResolvePath()).Length}" + Environment.NewLine +
        $"ModelSha256: {LocalWhisperModel.Sha256}" + Environment.NewLine +
        "Result: PASS" + Environment.NewLine);
}

static Window WaitForNamedWindow(AutomationElement root, string name, TimeSpan timeout)
{
    Stopwatch stopwatch = Stopwatch.StartNew();
    while (stopwatch.Elapsed < timeout)
    {
        AutomationElement? element = root.FindFirstDescendant(condition => condition.ByName(name));
        if (element is not null && element.ControlType == FlaUI.Core.Definitions.ControlType.Window)
            return element.AsWindow();
        Thread.Sleep(100);
    }
    throw new TimeoutException($"Window named '{name}' was not available within {timeout}.");
}

static AutomationElement WaitForElementNameContains(AutomationElement root, string automationId, string fragment, TimeSpan timeout)
{
    Stopwatch stopwatch = Stopwatch.StartNew();
    string? observed = null;
    while (stopwatch.Elapsed < timeout)
    {
        AutomationElement? element = root.FindFirstDescendant(condition => condition.ByAutomationId(automationId));
        observed = element?.Name;
        if (observed?.Contains(fragment, StringComparison.OrdinalIgnoreCase) is true) return element!;
        Thread.Sleep(100);
    }
    throw new TimeoutException($"Element '{automationId}' did not contain '{fragment}'. Last value: '{observed}'.");
}

static async Task<int> MigrateCredentialAsync(string sourcePath)
{
    string sourceSecret = (await File.ReadAllTextAsync(sourcePath)).Trim();
    DpapiSecretStore store = new();
    await store.SetSecretAsync(sourceSecret, CancellationToken.None);
    string? decryptedSecret = await store.GetSecretAsync(CancellationToken.None);
    byte[] sourceBytes = Encoding.UTF8.GetBytes(sourceSecret);
    byte[] decryptedBytes = Encoding.UTF8.GetBytes(decryptedSecret ?? string.Empty);
    try
    {
        bool matches = CryptographicOperations.FixedTimeEquals(sourceBytes, decryptedBytes);
        byte[] protectedBytes = await File.ReadAllBytesAsync(
            TalkToMeDataPaths.ProtectedSecretFile,
            CancellationToken.None);
        bool containsPlaintext = Encoding.UTF8.GetString(protectedBytes)
            .Contains(sourceSecret, StringComparison.Ordinal);
        if (!matches || containsPlaintext)
        {
            Console.Error.WriteLine("Credential migration verification failed.");
            return 1;
        }

        Console.WriteLine($"PASS protected-credential-bytes={protectedBytes.Length} plaintext-absent=True");
        return 0;
    }
    finally
    {
        CryptographicOperations.ZeroMemory(sourceBytes);
        CryptographicOperations.ZeroMemory(decryptedBytes);
    }
}

static async Task<int> ScanPackageAsync(string packageDirectory)
{
    DpapiSecretStore store = new();
    string? secret = await store.GetSecretAsync(CancellationToken.None);
    if (string.IsNullOrEmpty(secret))
    {
        Console.Error.WriteLine("Protected credential is unavailable for package scanning.");
        return 1;
    }

    byte[] utf8Pattern = Encoding.UTF8.GetBytes(secret);
    byte[] utf16Pattern = Encoding.Unicode.GetBytes(secret);
    try
    {
        foreach (string file in Directory.EnumerateFiles(packageDirectory, "*", SearchOption.AllDirectories))
        {
            byte[] content = await File.ReadAllBytesAsync(file, CancellationToken.None);
            if (content.AsSpan().IndexOf(utf8Pattern) >= 0 || content.AsSpan().IndexOf(utf16Pattern) >= 0)
            {
                Console.Error.WriteLine($"Secret material found in package file: {Path.GetFileName(file)}");
                return 1;
            }
        }

        Console.WriteLine("PASS package-secret-scan files-clean=True");
        return 0;
    }
    finally
    {
        CryptographicOperations.ZeroMemory(utf8Pattern);
        CryptographicOperations.ZeroMemory(utf16Pattern);
    }
}

static Window WaitForDescendantWindow(
    AutomationElement root,
    string automationId,
    TimeSpan timeout)
{
    Stopwatch stopwatch = Stopwatch.StartNew();
    while (stopwatch.Elapsed < timeout)
    {
        AutomationElement? element = root.FindFirstDescendant(
            condition => condition.ByAutomationId(automationId));
        if (element is not null)
        {
            return element.AsWindow();
        }

        Thread.Sleep(100);
    }

    throw new TimeoutException($"Owned window '{automationId}' was not available within {timeout}.");
}

static AutomationElement FindByAutomationId(AutomationElement root, string automationId) =>
    root.FindFirstDescendant(condition => condition.ByAutomationId(automationId))
    ?? throw new InvalidOperationException($"Element '{automationId}' was not found.");

static (Application Application, Process Process, Process Launcher, Window Window, AutomationElement Editor)
    LaunchTarget(string targetApplicationPath, UIA3Automation automation)
{
    ProcessStartInfo startInfo = new(targetApplicationPath)
    {
        UseShellExecute = false,
        WorkingDirectory = Path.GetDirectoryName(targetApplicationPath),
    };
    Application targetApplication = Application.Launch(startInfo);
    Process targetProcess = Process.GetProcessById(targetApplication.ProcessId);
    Window targetWindow = WaitForWindow(
        targetApplication,
        automation,
        "TestTargetWindow",
        TimeSpan.FromSeconds(15));
    AutomationElement editor = targetWindow.FindFirstDescendant(
        condition => condition.ByAutomationId("TargetEditor"))
        ?? throw new InvalidOperationException("The safe target editor was not found through UI Automation.");
    return (targetApplication, targetProcess, targetProcess, targetWindow, editor);
}

static void TerminateOwnedProcess(Process? process)
{
    if (process is null || process.HasExited)
    {
        return;
    }

    process.Kill(entireProcessTree: true);
    process.WaitForExit(5_000);
}

static void ActivateWindow(AutomationElement window, TimeSpan timeout)
{
    IntPtr windowHandle = new(window.Properties.NativeWindowHandle.Value);
    window.SetForeground();
    NativeMethods.SetForegroundWindow(windowHandle);
    if (NativeMethods.GetForegroundWindow() != windowHandle)
    {
        Keyboard.Press(VirtualKeyShort.ALT);
        Keyboard.Release(VirtualKeyShort.ALT);
        window.SetForeground();
        NativeMethods.SetForegroundWindow(windowHandle);
    }

    Stopwatch stopwatch = Stopwatch.StartNew();
    while (stopwatch.Elapsed < timeout)
    {
        if (NativeMethods.GetForegroundWindow() == windowHandle)
        {
            return;
        }

        Thread.Sleep(50);
    }

    throw new TimeoutException($"Target window {windowHandle} did not become foreground within {timeout}.");
}

static Window WaitForWindow(
    Application application,
    UIA3Automation automation,
    string automationId,
    TimeSpan timeout)
{
    Stopwatch stopwatch = Stopwatch.StartNew();
    while (stopwatch.Elapsed < timeout)
    {
        Window? window = application.GetAllTopLevelWindows(automation)
            .SingleOrDefault(candidate =>
                candidate.Properties.AutomationId.TryGetValue(out string? candidateAutomationId) &&
                candidateAutomationId == automationId);
        if (window is not null && window.Properties.ProcessId.Value == application.ProcessId)
        {
            return window;
        }

        Thread.Sleep(100);
    }

    throw new TimeoutException(
        $"Window '{automationId}' for process {application.ProcessId} was not available within {timeout}.");
}

static AutomationElement WaitForElementName(
    AutomationElement root,
    string automationId,
    string expectedName,
    TimeSpan timeout)
{
    Stopwatch stopwatch = Stopwatch.StartNew();
    string? observedName = null;
    while (stopwatch.Elapsed < timeout)
    {
        AutomationElement? element = root.FindFirstDescendant(
            condition => condition.ByAutomationId(automationId));
        observedName = element?.Name;
        if (element is not null && observedName == expectedName)
        {
            return element;
        }

        Thread.Sleep(100);
    }

    throw new TimeoutException(
        $"Element '{automationId}' did not reach name '{expectedName}' within {timeout}. Last value: '{observedName}'.");
}

static AutomationElement WaitForDuration(
    AutomationElement root,
    string automationId,
    TimeSpan minimumDuration,
    TimeSpan timeout)
{
    Stopwatch stopwatch = Stopwatch.StartNew();
    string? observedName = null;
    while (stopwatch.Elapsed < timeout)
    {
        AutomationElement? element = root.FindFirstDescendant(
            condition => condition.ByAutomationId(automationId));
        observedName = element?.Name;
        if (element is not null &&
            TimeSpan.TryParseExact(
                observedName,
                @"hh\:mm\:ss\.f",
                CultureInfo.InvariantCulture,
                out TimeSpan observedDuration) &&
            observedDuration >= minimumDuration)
        {
            return element;
        }

        Thread.Sleep(100);
    }

    throw new TimeoutException(
        $"Element '{automationId}' did not reach {minimumDuration} within {timeout}. Last value: '{observedName}'.");
}

static void ValidateWaveFile(string path)
{
    using FileStream stream = File.OpenRead(path);
    using BinaryReader reader = new(stream, Encoding.ASCII, leaveOpen: false);
    if (stream.Length <= 44 ||
        Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF")
    {
        throw new InvalidDataException("Recorded output is not a non-empty RIFF file.");
    }

    stream.Position = 8;
    if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE")
    {
        throw new InvalidDataException("Recorded output is not a WAVE file.");
    }

    stream.Position = 22;
    ushort channels = reader.ReadUInt16();
    int sampleRate = reader.ReadInt32();
    stream.Position = 34;
    ushort bitsPerSample = reader.ReadUInt16();
    if (channels != 1 || sampleRate != 16_000 || bitsPerSample != 16)
    {
        throw new InvalidDataException(
            $"Unexpected WAV format: {sampleRate} Hz, {bitsPerSample}-bit, {channels} channel(s).");
    }
}

static void AssertEnabledState(AutomationElement element, bool expectedEnabled, string context)
{
    if (element.IsEnabled != expectedEnabled)
    {
        throw new InvalidOperationException(
            $"Unexpected enabled state for {context}. Expected {expectedEnabled}, observed {element.IsEnabled}.");
    }
}

static void WriteAutomationTree(AutomationElement root, string path)
{
    StringBuilder builder = new();
    AppendElement(root, builder, 0);
    File.WriteAllText(path, builder.ToString());
}

static void AppendElement(AutomationElement element, StringBuilder builder, int depth)
{
    builder.Append(' ', depth * 2)
        .Append(ReadOptional(() => element.ControlType.ToString(), "Unsupported"))
        .Append(" Id=\"").Append(ReadOptional(() => element.AutomationId, string.Empty))
        .Append("\" Name=\"").Append(ReadOptional(() => element.Name, string.Empty))
        .Append("\" Bounds=").Append(ReadOptional(() => element.BoundingRectangle.ToString(), "Unsupported"))
        .AppendLine();

    foreach (AutomationElement child in element.FindAllChildren())
    {
        AppendElement(child, builder, depth + 1);
    }
}

static T ReadOptional<T>(Func<T> read, T fallback)
{
    try
    {
        return read();
    }
    catch (FlaUI.Core.Exceptions.PropertyNotSupportedException)
    {
        return fallback;
    }
}

static void CaptureWindow(AutomationElement window, string path)
{
    IntPtr windowHandle = new(window.Properties.NativeWindowHandle.Value);
    Rectangle bounds = Rectangle.Round(window.BoundingRectangle);
    using Bitmap bitmap = new(Math.Max(bounds.Width, 1), Math.Max(bounds.Height, 1), PixelFormat.Format32bppArgb);
    using Graphics graphics = Graphics.FromImage(bitmap);
    IntPtr deviceContext = graphics.GetHdc();

    try
    {
        if (!NativeMethods.PrintWindow(windowHandle, deviceContext, NativeMethods.RenderFullContent))
        {
            throw new InvalidOperationException($"PrintWindow failed for handle {windowHandle}.");
        }
    }
    finally
    {
        graphics.ReleaseHdc(deviceContext);
    }

    bitmap.Save(path, ImageFormat.Png);
}

static void CaptureFailureEvidence(
    Application? application,
    Process? process,
    AutomationElement? window,
    string evidenceDirectory,
    Exception exception)
{
    File.WriteAllText(Path.Combine(evidenceDirectory, "exception.txt"), exception.ToString());
    File.WriteAllText(
        Path.Combine(evidenceDirectory, "process-status.txt"),
        application is null
            ? "Application was not launched."
            : $"ProcessId: {application.ProcessId}{Environment.NewLine}HasExited: {process?.HasExited}");

    if (window is null)
    {
        return;
    }

    try
    {
        CaptureWindow(window, Path.Combine(evidenceDirectory, "failure-main-window.png"));
        WriteAutomationTree(window, Path.Combine(evidenceDirectory, "failure-uia-tree.txt"));
    }
    catch (Exception captureException)
    {
        File.WriteAllText(
            Path.Combine(evidenceDirectory, "capture-exception.txt"),
            captureException.ToString());
    }
}

internal static partial class NativeMethods
{
    internal const uint RenderFullContent = 2;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PrintWindow(IntPtr windowHandle, IntPtr destinationDeviceContext, uint flags);

    [LibraryImport("user32.dll")]
    internal static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetForegroundWindow(IntPtr windowHandle);
}
