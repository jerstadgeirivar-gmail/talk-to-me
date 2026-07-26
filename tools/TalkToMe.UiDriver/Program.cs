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

bool settingsScenario = args.Length == 3 && args[1] == "--settings";
bool liveAzureScenario = args.Length == 5 && args[3] == "--live";
if (!settingsScenario && args.Length is not 3 and not 5)
{
    Console.Error.WriteLine(
        "Usage: TalkToMe.UiDriver <application-path> <audio-fixture-path> <evidence-directory> [diagnostic-transcript|--live target-application-path]");
    return 2;
}

string applicationPath = Path.GetFullPath(args[0]);
string? audioFixturePath = settingsScenario ? null : Path.GetFullPath(args[1]);
string evidenceDirectory = Path.GetFullPath(args[2]);
string? expectedTranscript = !settingsScenario && !liveAzureScenario && args.Length == 5 ? args[3] : null;
string? targetApplicationPath = args.Length == 5 ? Path.GetFullPath(args[4]) : null;
bool insertionScenario = expectedTranscript is not null || liveAzureScenario;
Directory.CreateDirectory(evidenceDirectory);
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
    if (settingsScenario)
    {
        startInfo.ArgumentList.Add("--diagnostic-data-directory");
        startInfo.ArgumentList.Add(Path.Combine(evidenceDirectory, "data"));
    }
    else
    {
        startInfo.ArgumentList.Add("--diagnostic-audio");
        startInfo.ArgumentList.Add(audioFixturePath!);
        startInfo.ArgumentList.Add("--diagnostic-output");
        startInfo.ArgumentList.Add(recordingPath);
        startInfo.ArgumentList.Add("--diagnostic-speed");
        startInfo.ArgumentList.Add("2");
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
    if (settingsScenario)
    {
        RunSettingsScenario(application, automation, window, evidenceDirectory);
        Console.WriteLine($"PASS pid={processId} settings=protected-and-removed");
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
        Keyboard.TypeSimultaneously([VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.F9]);
    }

    WaitForElementName(window, statusAutomationId, recordingStatus, TimeSpan.FromSeconds(5));
    AutomationElement duration = WaitForDuration(
        window,
        durationAutomationId,
        liveAzureScenario ? TimeSpan.FromSeconds(24) : TimeSpan.FromSeconds(2),
        liveAzureScenario ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(10));
    AutomationElement stopButton = window.FindFirstDescendant(
        condition => condition.ByAutomationId(stopButtonAutomationId))
        ?? throw new InvalidOperationException($"Button '{stopButtonAutomationId}' was not found.");
    AssertEnabledState(button, expectedEnabled: false, "start button while recording");
    AssertEnabledState(stopButton, expectedEnabled: true, "stop button while recording");
    Stopwatch transcriptionStopwatch = Stopwatch.StartNew();
    if (!insertionScenario)
    {
        stopButton.AsButton().Invoke();
    }
    else
    {
        Keyboard.TypeSimultaneously([VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.F9]);
    }

    string expectedStatus = insertionScenario ? insertionCompleteStatus : recordOnlyStatus;
    AutomationElement status = WaitForElementName(
        window,
        statusAutomationId,
        expectedStatus,
        liveAzureScenario ? TimeSpan.FromMinutes(2) : TimeSpan.FromSeconds(5));
    transcriptionStopwatch.Stop();

    AssertEnabledState(button, expectedEnabled: true, "start button after recording");
    AssertEnabledState(stopButton, expectedEnabled: false, "stop button after recording");
    long recordingBytes = 0;
    if (!insertionScenario)
    {
        ValidateWaveFile(recordingPath);
        recordingBytes = new FileInfo(recordingPath).Length;
    }

    bool exactInsertionMatch = false;
    int transcriptCharacters = 0;
    int semanticAnchorCount = 0;
    if (insertionScenario)
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
        if (liveAzureScenario)
        {
            semanticAnchorCount = CountSemanticAnchors(displayedTranscript);
            if (semanticAnchorCount < 2)
            {
                throw new InvalidOperationException(
                    $"The live transcript contained only {semanticAnchorCount} expected semantic anchor(s).");
            }
        }

        AutomationElement insertButton = window.FindFirstDescendant(
            condition => condition.ByAutomationId(insertButtonAutomationId))
            ?? throw new InvalidOperationException($"Button '{insertButtonAutomationId}' was not found.");
        AssertEnabledState(insertButton, expectedEnabled: false, "insert button after automatic insertion");

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
    ];
    return anchors.Count(anchor => transcript.Contains(anchor, StringComparison.OrdinalIgnoreCase));
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

    AutomationElement endpoint = FindByAutomationId(settingsWindow, "AzureEndpointTextBox");
    AutomationElement deployment = FindByAutomationId(settingsWindow, "AzureDeploymentTextBox");
    AutomationElement apiVersion = FindByAutomationId(settingsWindow, "AzureApiVersionTextBox");
    AutomationElement password = FindByAutomationId(settingsWindow, "ApiKeyPasswordBox");
    AutomationElement save = FindByAutomationId(settingsWindow, "SaveSettingsButton");
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
    endpoint.AsTextBox().Text = "https://example.openai.azure.com/";
    apiVersion.AsTextBox().Text = "preview";
    password.Patterns.Value.Pattern.SetValue(syntheticKey);
    save.AsButton().Invoke();
    WaitForElementName(settingsWindow, "ApiKeyStatusText", "Configured", TimeSpan.FromSeconds(5));

    string dataDirectory = Path.Combine(evidenceDirectory, "data");
    string credentialPath = Path.Combine(dataDirectory, "credential.bin");
    string settingsPath = Path.Combine(dataDirectory, "settings.json");
    if (!File.Exists(credentialPath) || !File.Exists(settingsPath))
    {
        throw new InvalidOperationException("Settings or protected credential file was not created.");
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
    File.WriteAllText(
        Path.Combine(evidenceDirectory, "result.txt"),
        $"ProcessId: {application.ProcessId}{Environment.NewLine}" +
        "InvalidHttpsRejected: True" + Environment.NewLine +
        "ProtectedCredentialCreated: True" + Environment.NewLine +
        "PlaintextAbsent: True" + Environment.NewLine +
        "CredentialRemoved: True" + Environment.NewLine +
        "Result: PASS" + Environment.NewLine);
    settingsWindow.Close();
    if (!invokeTask.Wait(TimeSpan.FromSeconds(5)))
    {
        throw new TimeoutException("Settings dialog invocation did not complete after close.");
    }
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
            .SingleOrDefault(candidate => candidate.AutomationId == automationId);
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
