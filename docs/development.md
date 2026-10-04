# TalkToMe development guide

TalkToMe is a Windows 11 WPF utility for recording Norwegian dictation, transcribing locally by default, and inserting the completed text into the originally focused application. Azure OpenAI remains optional; LM Studio and Ollama profiles report their current speech-to-text limitation honestly.

In addition to the global hotkey, TalkToMe can listen locally for the English keyword **computer**. The first occurrence plays a short chime and starts recording; the next occurrence stops recording, plays the chime again, removes the closing keyword from the saved audio, and runs the normal transcription and insertion flow. This local listener is disabled by default and can be enabled in **Settings → Dictation preferences**. The same chime confirms recording start and stop when using the global hotkey. Only the completed dictation recording is sent to the configured Azure deployment.

## Prerequisites

- Windows 11
- .NET 10 SDK for development
- About 500 MB free disk space, 1.5 GB available RAM, and internet access for the one-time default model setup

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

Installers are produced by the manual **Build Windows installer** workflow in the repository's **Actions** tab. Choose **Run workflow** and optionally enter a three- or four-part version such as `1.2.0`. If no version is supplied, the workflow uses `1.0.<run number>`.

After the run completes, the workflow uploads the `TalkToMe-Setup-<version>` artifact and publishes the installer plus its SHA-256 checksum as the latest GitHub Release.

Installed Release builds use the authenticated GitHub CLI to check the release at startup, after Windows resumes, and once per hour. When TalkToMe is idle, it verifies the checksum, runs the installer silently, and restarts minimized. The main window also provides **Check for updates** and displays the running application version. Debug builds never update automatically.

Silent update diagnostics are written to `%LOCALAPPDATA%\TalkToMe\Updates\<version>\install.log`. The updater explicitly installs back into the directory of the running executable, so an isolated validation install cannot redirect a production update.

The resulting installer registers TalkToMe to start with Windows using the `--minimized` option, which keeps the main window hidden in the system tray.

The default global toggle is `Win+<`. The normal flow captures the foreground target on the first press and stops recording on the second. The main window can also start and stop recording when a separate target is not required.

## Configuration

Open **Settings → Transcription** to select and test a provider. Fresh installations select Local Whisper. Existing installations with complete Azure settings and no saved provider migrate once to Azure. Provider credentials are namespaced current-user DPAPI ciphertext under `%LOCALAPPDATA%\TalkToMe\Secrets`; they are never stored in `settings.json` or displayed after saving. See [configuration](configuration.md).

Open **Settings → Dictation** to select the transcription language mode. The persisted values are `auto`, `norwegian`, and `norwegian-english`; missing, null, or unknown values normalize to `norwegian`. The coordinator applies the selected language to the next normal transcription or retry. Explicit provider readiness contexts remain unchanged.

The small installer includes Whisper.net 1.9.1 and its CPU whisper.cpp runtime, but not the large model. On the first Local Whisper start, TalkToMe detects that the model is absent and asks permission to download and set up the pinned multilingual `small-q5_1` model (190,085,487 bytes; SHA-256 `ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb`). The download has progress and cancellation and is installed atomically only after verification. No Python, CUDA, account, or separate server is needed. The model is kept under `%LOCALAPPDATA%\TalkToMe\Models`; **Repair model** repeats the same verified setup flow.

Development overrides:

```text
TALKTOME_AZURE_ENDPOINT
TALKTOME_AZURE_API_KEY
TALKTOME_AZURE_DEPLOYMENT
TALKTOME_AZURE_API_VERSION
TALKTOME_WHISPER_MODEL_PATH
```

## Unattended validation

Installer validation must use a separate Windows user or disposable environment. The installer has a stable per-user Inno Setup `AppId`; installing it into an `artifacts` directory under the same user rewrites that user's production uninstall and startup registration.

For an intentional local install or upgrade of the current user's production copy, follow `.github/skills/install-talk-to-me/SKILL.md`. Its installer script always terminates all `TalkToMe.App` instances and verifies they are gone before setup starts.

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

See [manual-test-plan.md](manual-test-plan.md) and [troubleshooting.md](troubleshooting.md).

## Packaged validation record

The 2026-09-17 packaged validation used version `1.0.14`:

```powershell
$env:DOTNET_ROOT = 'C:\Users\Garg\.dotnet'
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
dotnet build .\TalkToMe.sln -c Release /p:Version=1.0.14
dotnet publish .\src\TalkToMe.App\TalkToMe.App.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\publish\win-x64 /p:Version=1.0.14
& 'C:\Users\Garg\AppData\Local\Programs\Antigravity IDE\resources\app\node_modules\innosetup\bin\ISCC.exe' /Q /DMyAppVersion=1.0.14 .\packaging\TalkToMe.iss
& .\.github\skills\install-talk-to-me\scripts\install-local.ps1 -InstallerPath .\artifacts\installer\TalkToMe-Setup.exe -ExpectedVersion 1.0.14 -LogPath .\artifacts\validation\install-1.0.14.log
```

The Release build and 30 tests passed. The installer compiled successfully with SHA-256 `98195170EE451962F37C516D7FA0E8C307E1B13AEF2E2553BFDC6ACB6E6592EC`, and installed `C:\Users\Garg\AppData\Local\Programs\TalkToMe\TalkToMe.App.exe` with file version `1.0.14.0`. Live installed-app evidence is retained in `artifacts\validation\installed-20260917-settings`, `installed-20260917-record-only`, `installed-20260917-privacy-transcription-failure`, and `installed-20260917-privacy-failure`; the installer log and checksum are `artifacts\validation\install-1.0.14.log` and `artifacts\installer\TalkToMe-Setup.exe.sha256`.

The automatic-update restart journey remains blocked because the fixed `1.0.14` package is not published and no existing updater harness can provide a valid release source containing these changes. Update-cancellation remains untested for the same reason; the installed-app journeys confirmed that no `gh.exe` process remained after each driver run.

### Local installation on 2026-10-04

Version `1.0.16` was published self-contained for `win-x64`, packaged with Inno Setup 6, and installed using `.github/skills/install-talk-to-me/scripts/install-local.ps1`. The script confirmed that no `TalkToMe.App` process remained before setup; the installer log at `artifacts/validation/install-1.0.16.log` reports success. The installed executable under `%LOCALAPPDATA%\Programs\TalkToMe` reports product version `1.0.16`. Installer SHA-256: `DBC19DDC6F742C1ACD635921A65627578FB6C2031651A0BED7A691A00DE3DA68`.

Real Debug and installed-binary recording and transcription-failure recovery journeys passed. Results, screenshots, and UI trees are under `artifacts/validation/local-install-1.0.16-debug-record`, `local-install-1.0.16-debug-failure`, `local-install-1.0.16-installed-record-final`, and `local-install-1.0.16-installed-failure`. An earlier installed recording attempt exited before exposing a window; the rerun passed after confirming no other app process remained.

The Windows 11 file-picker automation for `--import-file` must target the filename `Edit` with automation ID `1148` and the primary Open `SplitButton` with ID `1`. The first `Edit` in the dialog can belong to the file list, and the first element named `Open` is the filename history dropdown. The corrected driver closed the dialog and began import, but the installed Local Whisper provider reported failure for the MP3 fixture; the subsequent WAV import did not produce a completed result before the app exited. Neither real import transcription nor automatic-update restart was validated in this installation. Import investigation evidence is in `artifacts/validation/local-install-1.0.16-installed-import-final` and `local-install-1.0.16-installed-wav-import`.

## Limitations

- A normal-integrity process cannot inject input into an elevated target because of Windows UIPI.
- Microphone selection and a separately owned Windows 11 Notepad smoke test remain incomplete.
- Live Azure validation requires endpoint and deployment configuration in addition to the protected key.
- LM Studio 0.4.x and Ollama 0.31.1 official APIs were verified on 2026-07-31; neither documents an audio-transcription endpoint. Their profiles never send audio to chat/text endpoints.
- The binaries are unsigned and may trigger Microsoft Defender SmartScreen warnings.
