# TalkToMe

TalkToMe is a Windows 11 WPF utility for recording Norwegian dictation, transcribing through an Azure OpenAI deployment, and inserting the completed text into the originally focused application.

In addition to the global hotkey, TalkToMe can listen locally for the English
keyword **computer**. The first occurrence plays a short chime and starts
recording; the next occurrence stops recording, plays the chime again, removes
the closing keyword from the saved audio, and runs the normal transcription and
insertion flow. This local listener is disabled by default and can be enabled in
**Settings → Dictation preferences**. The same chime confirms recording start
and stop when using the global hotkey. Only the completed dictation recording
is sent to the configured Azure deployment.

## Prerequisites

- Windows 11
- .NET 10 SDK for development
- An Azure OpenAI transcription endpoint, deployment, and API key for live transcription

## Build

```powershell
dotnet restore TalkToMe.sln
dotnet build TalkToMe.sln --configuration Debug
dotnet test tests\TalkToMe.Infrastructure.Tests\TalkToMe.Infrastructure.Tests.csproj --configuration Debug
```

Launch the development build:

```powershell
src\TalkToMe.App\bin\Debug\net10.0-windows\TalkToMe.App.exe
```

Installers are produced by the manual **Build Windows installer** workflow in
the repository's **Actions** tab. Choose **Run workflow** and optionally enter a
three- or four-part version such as `1.2.0`. If no version is supplied, the
workflow uses `1.0.<run number>`.

After the run completes, the workflow uploads the `TalkToMe-Setup-<version>`
artifact and publishes the installer plus its SHA-256 checksum as the latest
private GitHub Release.

Installed Release builds use the authenticated GitHub CLI to check that private
release at startup, after Windows resumes, and once per hour. When TalkToMe is
idle, it verifies the checksum, runs the installer silently, and restarts
minimized. The main window also provides **Check for updates** and displays the
running application version. Debug builds never update automatically.

The resulting installer registers TalkToMe to start with Windows using the
`--minimized` option, which keeps the main window hidden in the system tray.

The default global toggle is `Ctrl+Alt+F9`. The normal flow captures the foreground target on the first press and stops recording on the second. The main window can also start and stop recording when a separate target is not required.

## Configuration

Open **Innstillinger** to save the endpoint and deployment. API keys are stored as current-user DPAPI ciphertext under `%LOCALAPPDATA%\TalkToMe`; they are never stored in `settings.json` or displayed after saving.

Development overrides:

```text
TALKTOME_AZURE_ENDPOINT
TALKTOME_AZURE_API_KEY
TALKTOME_AZURE_DEPLOYMENT
TALKTOME_AZURE_API_VERSION
```

## Unattended Validation

Record-only scenario:

```powershell
dotnet run --project tools\TalkToMe.UiDriver\TalkToMe.UiDriver.csproj --no-build -- src\TalkToMe.App\bin\Debug\net10.0-windows\TalkToMe.App.exe _init\norwegian-audio-speech-test.mp3 artifacts\validation\record-only
```

External insertion scenario:

```powershell
dotnet run --project tools\TalkToMe.UiDriver\TalkToMe.UiDriver.csproj --no-build -- src\TalkToMe.App\bin\Debug\net10.0-windows\TalkToMe.App.exe _init\norwegian-audio-speech-test.mp3 artifacts\validation\insertion "Dette er en test av norsk diktering." tools\TalkToMe.TestTarget\bin\Debug\net10.0-windows\TalkToMe.TestTarget.exe
```

Settings and protected-storage scenario:

```powershell
dotnet run --project tools\TalkToMe.UiDriver\TalkToMe.UiDriver.csproj --no-build -- src\TalkToMe.App\bin\Debug\net10.0-windows\TalkToMe.App.exe --settings artifacts\validation\settings
```

See [docs/manual-test-plan.md](docs/manual-test-plan.md) and [docs/troubleshooting.md](docs/troubleshooting.md).

## Limitations

- A normal-integrity process cannot inject input into an elevated target because of Windows UIPI.
- Microphone selection and a separately owned Windows 11 Notepad smoke test remain incomplete.
- Live Azure validation requires endpoint and deployment configuration in addition to the protected key.
- The binaries are unsigned and may trigger Microsoft Defender SmartScreen warnings.
