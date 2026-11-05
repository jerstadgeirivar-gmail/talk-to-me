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

Standalone vocabulary validation uses the real configured Azure credential in an isolated data directory and real imported audio. It checks legacy migration, external-edit preservation when settings are saved, empty/missing files, invalid UTF-8 failure, and transcription after repair:

```powershell
dotnet run --project tools\TalkToMe.UiDriver\TalkToMe.UiDriver.csproj --no-build -- src\TalkToMe.App\bin\Debug\net10.0-windows\TalkToMe.App.exe _init\norwegian-audio-speech-test.mp3 artifacts\validation\vocabulary-debug --vocabulary
```

Use a fresh evidence directory each time, and stop other `TalkToMe.App` instances first. The Windows picker helper accepts the primary Open control with automation ID `1` as either a Button or SplitButton, and selects the filename Edit with ID `1148`. The driver fails immediately on terminal import errors, bounds the normal import wait at three minutes, and closes its owned app in `finally`. Re-import after a recoverable failure returns through Idle, allowing repair-and-retry without restarting. A successful run records migration/recovery results, UI trees, screenshots, and the real transcript. It does not measure vocabulary accuracy improvement or establish provider token limits.

The 2026-11-01 Debug vocabulary journey passed in `artifacts/validation/vocabulary-debug-20261101-r4`: migration, external edits preserved by settings saves, empty/missing files, invalid UTF-8 failure, and real Azure transcription after repair. The returned transcript contained 203 characters, the source audio was preserved, and a post-run process query confirmed no app or driver remained. Earlier stalled-run evidence is retained under `artifacts/validation/vocabulary-stalled-20261101`; its observable status was `Audio import is not ready`, which led to the re-import recovery fix.

Version `1.0.19` was published self-contained and installed locally on 2026-11-01 with the required process-stop/verification script. Installer SHA-256: `E633D403A4EBF972A4B5DB720FF437D199ED1FC1F7B6A7F41D10D618C5FCA018`. The installation log is `artifacts/validation/vocabulary-install-1.0.19.log`. The same installed-binary migration, file failure/recovery, and real Azure transcription journey passed in `artifacts/validation/vocabulary-installed-20261101`. A separate real configured import in `artifacts/validation/vocabulary-personal-20261101` migrated the user's existing vocabulary exactly (54 lines, 1,076 characters) and cleared the inline settings value. Normal installed launch was responsive (`Ready`), and the Settings editor action opened the actual UTF-8 text in Notepad; inspected screenshots are in `artifacts/validation/vocabulary-normal-20261101`.

No vocabulary accuracy comparison, maximum-token boundary, Local Whisper transcription, file-permission denial, or automatic-update restart was exercised in this change. Tests used real WPF processes, real files, the real configured Azure service, and actual recorded audio; no unit tests or diagnostic transcript substitutes were used. The normal installed app and personal Notepad tab were left open intentionally after validation.

## Packaged validation record

### Endpoint profiles and PCM segmentation, version 1.0.21

Version 1.0.21 replaces whole-file MP3 preparation with configured PCM segmentation and adds a first-class endpoint registry in Settings. Each profile owns its limits, route/authentication choices and DPAPI credential; duplication does not duplicate credentials. Existing Azure fields/key migrate into a legacy-compatible profile. Settings saves apply to the next job without waiting for an active transcription. See [configuration](configuration.md#registered-transcription-endpoints) for exact defaults, planning formula and checkpoint privacy.

The exact two-hour real LibriVox audiobook excerpt completed in the Debug app through the user's configured Azure service. Evidence is `artifacts/validation/segmentation-two-hours-debug-20261105`: 7,200 seconds of submitted audio coverage without gaps, 49 completed segments, two completed segments reused after cancellation/restart, 50 requests started including the interrupted request, maximum upload 4,796,846 bytes, 108,066 transcript characters, unchanged source hash, DPAPI-protected progress, successful cleanup and transcript/key-free logs. The source is `artifacts/validation/public-domain-audio/pride-and-prejudice-two-hours.wav`; it contains genuine human speech, not repeated short audio. The ready-window screenshot and endpoint editor screenshot were inspected. This is bounded-process and source-coverage evidence, not an exhaustive transcription-accuracy benchmark.

The shorter dense-speech journey in `artifacts/validation/segmentation-progress-debug-20261105` additionally exercised actual returned token usage exceeding the configured budget, adaptive subdivision, cancellation and encrypted restart recovery. Visible progress retains segment number and audio position while sending and waiting. No unit tests, mocks, fake executables or simulated service responses were used.

The self-contained win-x64 1.0.21 installer was built with Inno Setup 6 and installed using the required stop/query/install script. SHA-256: `C212E09DB74290C82E1EAE79FAD18D286117C04AF657A0C8421DD730C7E8FF16`. `artifacts/validation/segmentation-install-1.0.21.log` confirms success without a Windows restart and the installed executable reports 1.0.21. Short installed-binary journeys passed in `segmentation-profile-installed-20261105` (profile registry, credential isolation, token-budget reduction, cancellation/restart, nonempty real transcript, source integrity and privacy), `segmentation-azure-repair-installed-20261105` (real HTTP 404, Settings repair and Retry), and `segmentation-provider-installed-20261105-r3` (real saved-settings/key readiness test). Independent normal launch in `segmentation-normal-installed-20261105` was responsive, displayed recovered audio, and successfully invoked the diagnostics-folder action. The installed app was left open.

The first normal-provider driver checks rejected the new intentional scrolling layout and then used an incorrect selector automation ID. Those test assertions were repaired, not hidden; earlier failed evidence remains under `segmentation-provider-installed-20261105` and `-r2`. The successful check now asserts enabled provider/profile/test/save controls instead of forbidding scrolling. Product profile selection and the editor's visible content were inspected in real screenshots.

Repeat focused checks with fresh evidence directories, no competing app instance, and real endpoint credentials:

```powershell
dotnet build TalkToMe.sln -c Debug
dotnet run --project tools\TalkToMe.UiDriver\TalkToMe.UiDriver.csproj --no-build -- <app.exe> <real-dense-chapter.mp3> artifacts\validation\segment-recovery --segmentation-token-limit
dotnet run --project tools\TalkToMe.UiDriver\TalkToMe.UiDriver.csproj --no-build -- <app.exe> <exact-two-hour.wav> artifacts\validation\segment-two-hours --segmentation-long
dotnet run --project tools\TalkToMe.UiDriver\TalkToMe.UiDriver.csproj --no-build -- <app.exe> _init\norwegian-audio-speech-test.mp3 artifacts\validation\segment-azure-repair --azure-error-retry
```

The two-hour paid run was executed once against Debug. Installed validation deliberately used the shorter chapter and readiness/failure clips rather than repeating two-hour billing. The seven-hour source remains an interrupted run, not a successful full-file test. Non-Azure compatible services, endpoint-specific retirement/context contracts, exhaustive word/boundary accuracy, microphone capture, clipboard insertion, disk-full/permission recovery, live HTTP 429/timeout retry, files longer than two hours and automatic-update restart were not exercised for this change. Providers without output usage or a truncation signal cannot be certified complete solely from nonempty text. Keep these limitations explicit when reporting validation.

### Validation cost ceiling, 2026-11-05

Long paid transcription validation is capped at two hours per input. The `--segmentation-long` driver checks decoded duration before writing isolated credentials, launching TalkToMe, or sending Azure requests, and rejects files longer than two hours. This is a validation cost guard, not a production limit on supported recording length.

The earlier `artifacts/validation/segmentation-multihour-debug-20261105` run used a 7:09:55.6 public-domain audiobook and was stopped at the user's request. All app and driver processes were terminated. Its retained runtime events show 60 requests started and 59 completed segments, with the last completed interval ending at 8,772 seconds (2:26:12). Cancellation cannot guarantee that an already-submitted request incurs no charge. This interrupted run is not a completed seven-hour end-to-end validation, and its billable portions should not be repeated unnecessarily.

Offline extraction through the existing NAudio dependency created `artifacts/validation/public-domain-audio/pride-and-prejudice-two-hours.wav`: exactly 7,200 seconds, 230,400,046 bytes, with zero Azure requests. The original M4B was preserved. The extraction command refuses durations above two hours and refuses to overwrite existing files:

```powershell
dotnet run --project tools\TalkToMe.UiDriver\TalkToMe.UiDriver.csproj --no-build -- --extract-audio <source-audio> <new-output.wav> 7200
```

The actual seven-hour source was then rejected by the long-validation preflight with exit code 2 and no app launch. At the cost-correction checkpoint, the two-hour excerpt was prepared but not transcribed. After the user explicitly requested completion, one exact two-hour Debug journey was run successfully, as recorded above. No seven-hour job was resumed and no two-hour run was repeated against the installed binary.

### Long-audio recovery and diagnostics, 2026-11-05

The real retained 13:08.28 recording reproduced the version 1.0.19 failure before any Azure request: the requested 32 kbit/s native MP3 format was unavailable. The fixed Debug build negotiated 48 kbit/s, reduced 25,225,006 bytes to 4,730,615 bytes, and received a nonempty 3,604-character transcript. The original WAV and its existing copy were preserved. Private transcripts and screenshots are kept only under ignored `artifacts/validation`; do not commit them.

Real Debug journeys passed in `artifacts/validation/transcription-diagnostics-debug-20261105`, `transcription-retry-debug-final-20261105`, and `transcription-azure-error-debug-20261105`. These cover the original long recording, invalid UTF-8 vocabulary failure and repair followed by Retry, and an actual Azure HTTP 404 for an isolated nonexistent deployment followed by Settings repair and Retry. Source SHA-256 remained unchanged; log checks excluded the protected key and full transcript and required failure/completion events. Screenshots of the ready transcript and both failure messages were inspected. The solution builds without warnings; no unit tests or test doubles were run.

Version `1.0.20` was published self-contained, packaged with Inno Setup 6, and installed into `%LOCALAPPDATA%\Programs\TalkToMe` using the mandatory stop/verify/install script. Installer SHA-256: `106BCC27215C7CCDA8F0E65159DB0F1864DD0827AF34071996FF226CD8A4DD80`. The installer log at `artifacts/validation/transcription-install-1.0.20.log` confirms success without a Windows restart. The same long-audio/vocabulary Retry and short-audio/Azure HTTP 404 repair journeys passed against the exact installed binary in `artifacts/validation/transcription-retry-installed-20261105` and `transcription-azure-error-installed-20261105`; source hash and log privacy checks passed again.

Normal installed launch was independently inspected in `artifacts/validation/transcription-normal-installed-20261105`. The diagnostics button opened an actual Explorer window at `%LOCALAPPDATA%\TalkToMe\Diagnostics`. A normal real configured import then returned 3,647 characters, left the installed app responsive and open with the transcript visible, and saved its text/screenshot/UIA evidence in `artifacts/validation/transcription-ready-installed-20261105`. Both existing WAVs remained present with SHA-256 `A1692FB39B72393C58D23E5A28836119C70A03247A9BA92F06C87A7E8DC3897E`. Transcript text varies across independent Azure requests; the 3,604-character count above refers to the first successful recovery. No commit or push was made.

Repeat with a fresh evidence directory and no other app instance:

```powershell
dotnet run --project tools\TalkToMe.UiDriver\TalkToMe.UiDriver.csproj --no-build -- <app.exe> <actual-long.wav> artifacts\validation\long-audio-retry --vocabulary-retry
dotnet run --project tools\TalkToMe.UiDriver\TalkToMe.UiDriver.csproj --no-build -- <app.exe> _init\norwegian-audio-speech-test.mp3 artifacts\validation\azure-error-retry --azure-error-retry
```

These modes copy real Azure settings and the protected credential into isolated data directories. The first validates existing vocabulary migration/read failure/recovery and then invokes Retry on the same imported audio; the second requests an intentionally nonexistent deployment without creating or changing any Azure resources, repairs it through Settings, and retries. The production settings and vocabulary are not modified. Normal small-audio success also exercises the compression bypass. Logs in each isolated `data/Diagnostics` directory contain the real Azure request IDs and stage timings.

To leave a normally launched app open with a recovered transcript, use `--inspect-running <evidence-directory> --import-audio <actual-audio-path>`. This attaches to the existing process, imports through its real file picker, verifies a nonempty transcript and unchanged source hash, and saves evidence without closing the app. `--inspect-running <evidence-directory> --open-diagnostics` invokes the diagnostics-folder button. No microphone capture, clipboard insertion, Local Whisper fallback, unavailable Media Features, request timeout/rate-limit, or automatic-update restart was exercised in this change.

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
