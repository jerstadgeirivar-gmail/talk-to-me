# TalkToMe

TalkToMe is a Windows 11 WPF utility for recording Norwegian dictation, transcribing through an Azure OpenAI deployment, and inserting the completed text into the originally focused application.

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
- Global hotkey customization, microphone selection, startup registration, retry UI, and a separately owned Windows 11 Notepad smoke test remain incomplete.
- Live Azure validation requires endpoint and deployment configuration in addition to the protected key.
- The binaries are unsigned and may trigger Microsoft Defender SmartScreen warnings.
