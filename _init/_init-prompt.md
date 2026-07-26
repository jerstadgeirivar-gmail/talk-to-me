# Build a Production-Quality Windows Voice Dictation Application

You are the principal software engineer responsible for designing, implementing, testing, documenting, packaging, and verifying a complete Windows desktop application.

Do not merely propose code or provide isolated snippets. Work directly in the repository, create the solution and all required files, execute builds and tests, diagnose failures, and leave the repository in a working, maintainable state.

The application is intended for private, daily use on a Windows 11 PC. It must allow the user to place the text cursor in almost any Windows application, activate dictation with a global keyboard shortcut, speak naturally in Norwegian for an extended period with arbitrary pauses, stop dictation manually, transcribe the recording through an existing Microsoft Foundry/Azure OpenAI transcription deployment, and insert the resulting text at the original cursor position.

The user already has:

* A Microsoft Foundry/Azure OpenAI transcription deployment.
* An API endpoint.
* An API key.
* A deployment that is either `gpt-4o-transcribe` or `gpt-4o-mini-transcribe`.
* Azure credits covering normal private usage.

Treat the actual deployment name, endpoint format, API version, and model version as configuration. Do not assume that the deployment name is identical to the underlying model name.

## 1. Core outcome

Build a polished Windows tray application with this primary interaction:

1. The user places the caret in a text field in an application such as VS Code, Notepad, a browser, Windows Terminal, Slack, Teams, or another editor.
2. The user presses a configurable global hotkey.
3. The application begins microphone recording without taking keyboard focus away from the target application.
4. A small non-activating overlay clearly indicates that recording is active and displays elapsed time.
5. The user may speak naturally in Norwegian, mix in English technical terminology, correct themselves, and remain silent for extended periods.
6. Recording continues until the user explicitly stops it. Silence must never end the recording automatically.
7. The user presses the global hotkey again.
8. The application stops recording and sends the complete audio recording to the configured Azure transcription deployment.
9. The overlay changes to a processing state.
10. When transcription succeeds, the application restores or verifies the intended target window and inserts the transcription at the caret.
11. The application returns to its idle state.
12. If insertion fails, the transcription must remain recoverable and be placed on the clipboard with a clear notification. Never silently lose a successful transcription.

The application must favor correctness, reliability, recoverability, and maintainability over unnecessary features.

## 2. Scope for version 1

Implement the following:

* Windows 11 desktop application.
* System tray operation.
* Global configurable toggle hotkey.
* Microphone selection.
* Reliable local audio recording.
* Manual start and stop.
* Long pauses without endpoint detection.
* Microsoft Foundry/Azure OpenAI transcription.
* Support for either `gpt-4o-transcribe` or `gpt-4o-mini-transcribe`, selected through the deployment configuration.
* Norwegian transcription with English technical terms.
* A customizable transcription context/glossary.
* Non-activating recording/processing overlay.
* Robust text insertion into the target application.
* Clipboard fallback.
* Settings UI.
* Secure API-key storage.
* Connection test.
* Local diagnostics that exclude audio, transcripts, clipboard contents, and secrets.
* Recovery of a pending recording after a transient API or network failure.
* Automated tests.
* Windows build and packaging instructions.
* Complete developer and user documentation.

## 3. Explicit non-goals for version 1

Do not add these unless they are essential to satisfy a core acceptance criterion:

* Wake-word detection.
* Always-listening microphone behavior.
* Voice-controlled start or stop.
* Speaker diarization.
* Meeting transcription.
* Cloud database.
* Custom backend server.
* User accounts or authentication beyond Azure API credentials.
* Multi-device synchronization.
* Translation.
* Automatic LLM rewriting or summarization.
* Electron, a browser-hosted UI, or a local web server.
* Telemetry sent to third parties.
* Complex plugin systems.
* Premature microservices or distributed architecture.

Design interfaces so selected future capabilities can be added, but do not implement speculative infrastructure.

## 4. Required technology direction

Use the following stack unless repository or platform inspection reveals a concrete incompatibility:

* C#.
* Current supported .NET LTS for Windows, preferably .NET 10.
* WPF for the desktop UI, tray behavior, settings, and overlay.
* `CommunityToolkit.Mvvm` where it provides clear value.
* `Microsoft.Extensions.Hosting` for application lifetime, dependency injection, configuration, and logging.
* NAudio or an equivalently mature Windows audio library using WASAPI for microphone capture.
* Official Microsoft/Azure/OpenAI .NET SDKs when they support the actual Foundry endpoint and Audio Transcription API correctly.
* `HttpClient` with a typed client if the current official SDK does not correctly support the deployed endpoint.
* Native Win32 APIs through carefully isolated interop code for global hotkeys, foreground-window tracking, focus restoration, and input injection.
* `System.Text.Json`.
* xUnit for automated tests.
* FluentAssertions only if its current license and use are appropriate; otherwise use native xUnit assertions.
* A Windows GitHub Actions workflow if the repository is hosted on GitHub.

Do not add dependencies merely to avoid writing a few lines of clear code. Prefer well-maintained, appropriately licensed packages. Record why every nontrivial dependency is necessary.

## 5. First action: inspect and verify

Before implementation:

1. Inspect the repository and current environment.
2. Preserve any existing user work.
3. Check for `AGENTS.md`, repository instructions, existing solution files, `.editorconfig`, and existing architectural conventions.
4. Determine which .NET SDKs and Windows build tools are available.
5. Consult current official Microsoft documentation for:

   * The supplied Microsoft Foundry/Azure OpenAI endpoint type.
   * The correct transcription endpoint or SDK method.
   * Authentication using an API key.
   * Required or supported API versions.
   * Supported audio formats and maximum upload size.
   * Language hint syntax.
   * Prompt/context support.
   * Streaming-response support, if any.
6. Do not guess an API route from memory.
7. Do not hard-code a preview API version without verifying it against the actual deployment.
8. Never print, echo, log, commit, or expose the API key.

Create a short implementation plan in `docs/implementation-plan.md`, then proceed with implementation. Do not stop after writing the plan.

If a critical value is missing, create configuration placeholders and continue everything that can be completed safely. Ask the user only when progress is genuinely blocked.

## 6. Configuration contract

Support the following logical configuration values:

* Azure endpoint.
* Azure API key.
* Azure deployment name.
* Azure API version, only when the endpoint requires it.
* Preferred transcription language.
* Optional transcription prompt/context.
* Optional glossary of technical terms.
* Selected microphone device.
* Global hotkey.
* Whether the transcript should be inserted into the window captured at recording start or the window active at completion.
* Whether pending audio should be retained after a failed transcription.
* Local diagnostic logging level.

Use development environment variables with names such as:

* `TALKTOME_AZURE_ENDPOINT`
* `TALKTOME_AZURE_API_KEY`
* `TALKTOME_AZURE_DEPLOYMENT`
* `TALKTOME_AZURE_API_VERSION`

Do not commit real values.

For normal installed use:

* Store the API key in Windows Credential Manager or an equivalently appropriate per-user Windows secret store.
* Store non-secret settings under the current user’s application-data directory.
* Never store the API key in plain-text JSON, source code, logs, command-line arguments, or the repository.
* Validate that the endpoint uses HTTPS.
* Provide a settings screen where the user can enter and test the endpoint, deployment name, and API key.
* Mask the API key in the UI.
* Do not reveal the existing key after it has been saved.
* Allow the saved credential to be replaced or removed.

Support environment-variable overrides for development and automated contract tests.

## 7. Suggested solution structure

Use a small, deliberate architecture. A suitable starting point is:

```text
TalkToMe.sln
src/
  TalkToMe.App/
  TalkToMe.Core/
  TalkToMe.Infrastructure/
tests/
  TalkToMe.Core.Tests/
  TalkToMe.Infrastructure.Tests/
  TalkToMe.App.Tests/
docs/
  implementation-plan.md
  architecture.md
  manual-test-plan.md
  troubleshooting.md
  adr/
```

Responsibilities:

### `TalkToMe.Core`

Contain platform-independent application logic and contracts:

* Dictation state machine.
* Recording-session model.
* Transcription request/result models.
* `ITranscriptionService`.
* `IAudioRecorder`.
* `ITextInsertionService`.
* `IForegroundTargetService`.
* `IHotkeyService`.
* `ISecretStore`.
* `IPendingRecordingStore`.
* Configuration validation.
* Error classifications.
* Application orchestration.

Do not reference WPF from this project.

### `TalkToMe.Infrastructure`

Contain:

* Azure transcription implementation.
* WASAPI/NAudio recording.
* Audio encoding.
* Win32 foreground-window handling.
* Global hotkey registration.
* Clipboard and input injection.
* Windows Credential Manager integration.
* Pending-recording persistence.
* Local file-system settings.
* Redacted logging support.

Keep native interop isolated, documented, and covered by tests where feasible.

### `TalkToMe.App`

Contain:

* WPF application startup and dependency composition.
* Tray icon.
* Settings window.
* Recording/processing overlay.
* View models.
* Notifications.
* User-facing error handling.
* Single-instance application behavior.

Avoid business logic in code-behind.

Do not create additional projects unless there is a concrete architectural reason.

## 8. Architecture and design principles

Apply:

* SOLID where it improves replaceability and testability.
* Separation of concerns.
* Dependency inversion around operating-system and Azure integrations.
* Explicit state transitions.
* Structured error handling.
* Cancellation support.
* Async I/O throughout network and file operations.
* Immutable result types where practical.
* Nullable reference types.
* Clear ownership and disposal of microphones, streams, temporary files, cancellation tokens, tray icons, and native handles.
* YAGNI and KISS alongside SOLID.
* Composition over inheritance.
* No service locator.
* No global mutable singleton state except through controlled application-lifetime services.
* No fire-and-forget tasks without explicit error observation.
* No `.Result`, `.Wait()`, or blocking async calls on the UI thread.
* No broad `catch (Exception)` that silently swallows errors.
* No duplicated business rules.

Do not produce ceremonial abstractions or a “clean architecture” with unnecessary layers. Each abstraction must correspond to a real boundary that needs testing or replacement.

Document significant decisions as concise ADRs, including:

* WPF selection.
* Audio capture and encoding choice.
* Azure client/API integration choice.
* Text insertion strategy.
* Credential-storage choice.
* Pending-recording recovery policy.

## 9. Dictation state machine

Implement a deterministic state machine. At minimum:

* `Unconfigured`
* `Idle`
* `StartingRecording`
* `Recording`
* `StoppingRecording`
* `Transcribing`
* `Inserting`
* `Completed`
* `RecoverableFailure`
* `FatalFailure`
* `Cancelling`

Define allowed transitions explicitly. Reject or ignore invalid duplicate commands safely.

Examples:

* Pressing the hotkey twice very quickly must not create two microphone sessions.
* A second stop request while already stopping must be harmless.
* Closing the application while recording must ask the user whether to discard or preserve the recording.
* Network failure must transition to a recoverable state with the audio retained.
* A successful transcript followed by insertion failure must retain the text.
* Cancelling must release the microphone and clean up according to the retention policy.

Expose state changes to the UI through observable, thread-safe mechanisms.

## 10. Audio recording

Implement audio capture for clear speech recognition:

* Default to a sensible mono speech format.
* Verify the required sample rate, bit depth, channel count, and accepted file formats against the actual Azure transcription contract.
* Prefer a compressed, speech-appropriate format if officially supported and reliably encodable on Windows.
* If WAV is used, calculate and enforce the provider’s upload-size limit.
* Display a warning before the current recording approaches the maximum supported request size.
* Do not wait until upload time to discover that the recording is too large.
* If robust compression is unavailable, implement safe segmentation at silence boundaries rather than truncating audio.
* Preserve segment order.
* Supply relevant preceding-text context to later segments if supported.
* Never silently omit audio.

The recorder must:

* Enumerate microphone devices.
* Persist the selected microphone.
* Recover gracefully if the default device changes.
* Report device-disconnected errors clearly.
* Avoid exclusive-mode capture unless there is a demonstrated requirement.
* Stop and dispose deterministically.
* Record arbitrary silence until manual stop.
* Avoid acoustic processing that materially damages transcription unless tested.
* Track duration and approximate encoded/request size.
* Write recoverable data incrementally so an application crash does not necessarily lose a long recording.

Store temporary or pending audio in a per-user application-data directory with user-only access where practical.

On successful transcription and insertion, delete temporary audio unless the user has explicitly selected a retention option. On failure, retain it for retry. Never place audio in the repository or a globally accessible temporary location.

## 11. Azure transcription integration

Implement `ITranscriptionService` so the application is independent of the specific configured deployment.

Requirements:

* Use the configured deployment name, not a hard-coded model name.
* Work with both `gpt-4o-transcribe` and `gpt-4o-mini-transcribe` deployments.
* Validate the configured endpoint.
* Use the official supported authentication mechanism.
* Respect provider request-size limits.
* Use a Norwegian language hint only if the deployed API officially supports it.
* Determine the correct Norwegian language code from the current API documentation; do not guess.
* Allow optional prompt/context and glossary input when supported.
* Preserve English technical terms and identifiers.
* Set explicit, appropriate timeouts for long uploads and transcription.
* Support cancellation.
* Classify authentication, authorization, quota, rate limit, invalid request, unsupported media, network, timeout, and server errors separately.
* Surface actionable user messages without exposing secrets or raw response bodies containing sensitive data.
* Record request ID, status code, duration, deployment identifier, audio duration, and failure category in diagnostics.
* Never log audio bytes or transcript text.

Retry policy:

* Retry transient connection failures and server throttling only when doing so is safe.
* Honor `Retry-After`.
* Use bounded exponential backoff with jitter.
* Do not retry authentication or validation failures.
* Do not perform unlimited retries.
* Be cautious with ambiguous failures after the server may already have processed the upload.
* Preserve audio and offer an explicit manual retry whenever request outcome is uncertain.

Provide a “Test connection” operation using a tiny local test recording or another official low-cost validation mechanism. It must verify:

* Endpoint reachability.
* Authentication.
* Deployment existence.
* API compatibility.
* Ability to receive a transcription.

Do not expose the API key in validation errors.

## 12. Norwegian and technical-language behavior

The default transcription configuration should describe the use case approximately as follows, adapted to the actual API capabilities:

* Primary language: Norwegian Bokmål.
* Occasional Norwegian dialect pronunciation.
* Frequent English software-development terminology.
* Preserve exact technical product names, file names, commands, identifiers, acronyms, version numbers, and paths.
* Add natural Norwegian punctuation and paragraph boundaries.
* Do not translate English technical terms into Norwegian unless naturally spoken that way.
* Do not fabricate code or commands.

Provide a user-editable glossary containing terms such as:

* Azure
* Microsoft Foundry
* GitHub Copilot
* Visual Studio Code
* PowerShell
* TypeScript
* JavaScript
* C#
* .NET
* Kubernetes
* Docker
* PostgreSQL
* REST
* API
* JSON
* YAML
* appsettings.json
* nullable
* async
* await

The glossary must be configuration, not embedded throughout the implementation.

Do not add a separate generative rewriting model in version 1. Return the transcription supplied by the transcription deployment with only deterministic normalization required for insertion, such as newline normalization. Avoid “correcting” technical content after transcription.

## 13. Foreground target and caret behavior

At recording start:

* Capture the foreground window handle.
* Capture enough non-sensitive metadata to validate the target later, such as process ID and window identity.
* Do not capture the window’s document content.
* Do not take focus.
* Keep the overlay non-activating.

At insertion:

* Validate that the captured target still exists.
* Follow the configured target policy.
* Restore the target only when appropriate.
* Avoid surprising focus theft if the user intentionally switched context.
* If the target cannot be safely restored, place the transcript on the clipboard and notify the user.
* Never insert into an unknown window merely because it became active during processing.

Account for Windows User Interface Privilege Isolation:

* A normal process cannot reliably inject input into an elevated process.
* Detect likely integrity-level mismatch where practical.
* Explain the limitation clearly.
* Do not run the entire application as administrator by default.
* Do not bypass Windows security boundaries.
* Treat support for elevated target applications as a future, separately reviewed capability.

## 14. Text insertion

Implement `ITextInsertionService` with a carefully selected Windows strategy.

The primary strategy may use Unicode clipboard paste plus `SendInput` for the paste shortcut because this is generally compatible across WPF, Win32, browsers, Electron, and editors. However, implementation must handle clipboard correctness carefully.

Requirements:

* Preserve the user’s existing clipboard where safely possible.
* Do not overwrite a clipboard value that the user changed concurrently.
* Use a short, bounded wait for clipboard contention.
* Restore the prior clipboard only if the clipboard still contains the application’s temporary value or identifiable marker.
* Avoid corrupting custom or delayed clipboard formats.
* If full clipboard restoration cannot be guaranteed, document the limitation and prioritize not losing the transcript.
* On insertion failure, leave the transcript on the clipboard and show a notification.
* Normalize line endings appropriately.
* Preserve Unicode Norwegian characters.
* Preserve multiline prompts.
* Do not append Enter or submit the prompt automatically.
* Do not simulate characters one by one unless used as a tested fallback.
* Never paste into password fields when they can be identified reliably.
* Provide a user setting to disable automatic insertion and use clipboard-only mode.

Use an injectable clipboard/native-input adapter so behavior can be tested without changing the developer’s real clipboard during unit tests.

## 15. Hotkey and application lifetime

Implement a configurable global hotkey through the supported Windows API.

Requirements:

* The default binding must be unlikely to conflict with Windows accessibility and system shortcuts.
* Detect registration failure.
* Explain conflicts and allow the user to choose another combination.
* Use the same hotkey to toggle recording.
* Support `Escape` to cancel when the overlay has an appropriate cancellation route, without globally hijacking Escape while idle.
* Unregister the hotkey reliably on shutdown.
* Prevent multiple application instances from registering duplicate hotkeys.
* A second launch should activate the existing settings/status UI rather than start another instance.
* Do not install a global low-level keyboard hook if `RegisterHotKey` satisfies the requirement.

## 16. User interface

Create a restrained, professional UI.

### Tray menu

Include:

* Start dictation.
* Stop dictation when active.
* Retry pending transcription when available.
* Copy last transcript.
* Open settings.
* Open diagnostics folder.
* Exit.

### Overlay

The overlay must:

* Never steal input focus.
* Show recording, processing, success, recoverable failure, and fatal error states distinctly.
* Show elapsed recording duration.
* Show an approximate size-limit warning.
* Provide a visible but nonintrusive microphone indicator.
* Be keyboard- and screen-reader-conscious where possible.
* Avoid excessive animation.
* Move intelligently if it would appear off-screen.
* Respect multiple monitors and DPI scaling.
* Use the active monitor.
* Disappear after successful insertion.
* Remain available with recovery actions after failure.

### Settings

Include:

* Azure endpoint.
* Deployment name.
* API version only if required.
* Masked API-key replacement field.
* Connection test.
* Microphone selection and input-level indicator.
* Hotkey configuration.
* Target-window behavior.
* Clipboard-only mode.
* Norwegian context/glossary editor.
* Pending-audio retention setting.
* Start with Windows option, implemented through a normal per-user mechanism.
* Diagnostic logging level.
* Clear pending audio.
* Remove stored credential.

Validate fields inline and prevent saving structurally invalid configuration.

## 17. Reliability and recovery

The application must assume that microphones, networks, windows, clipboards, and cloud APIs can fail.

Implement:

* Pending recording metadata.
* Retry after network restoration.
* Recovery after application restart.
* Safe cleanup of abandoned temporary files.
* A bounded retention policy.
* User confirmation before deleting pending recordings.
* Last successful transcript retained locally only if the user enables transcript history; transcript history should be disabled by default.
* Copy-last-transcript functionality for the current session.
* No silent data loss.

If the application crashes during recording:

* On next launch, detect recoverable audio.
* Explain what was recovered.
* Allow retry, export, or deletion.

Use atomic settings writes and safe file replacement.

## 18. Security and privacy

Perform a lightweight threat analysis and document it.

Protect:

* API key.
* Audio.
* Transcripts.
* Clipboard contents.
* Local logs.
* Pending recordings.
* Endpoint configuration.

Requirements:

* HTTPS-only endpoint validation.
* Windows per-user secret storage.
* No secrets in source control.
* No secrets in exceptions, logs, crash messages, screenshots, or process arguments.
* No audio or transcript logging.
* No clipboard logging.
* No analytics or telemetry by default.
* Redact query strings and authentication headers.
* Restrict pending files to the current user where practical.
* Delete temporary data after successful completion.
* Add `.gitignore` entries for all local secrets, recordings, build output, and user settings.
* Add a sample configuration containing placeholders only.
* Run a secret scan or at least inspect the final Git diff for accidental credentials.

If the user supplies a real API key during development, use it only through an environment variable or secure local store. Never insert it into a file through source generation.

## 19. Logging and diagnostics

Use structured local logging with event IDs.

Allowed diagnostic data:

* State transition.
* Timestamp.
* Application version.
* Operation duration.
* Audio duration.
* Approximate audio size.
* Selected device identifier, preferably non-sensitive.
* HTTP status.
* Azure request ID.
* Deployment name.
* Error category.
* Retry count.

Forbidden diagnostic data:

* API key.
* Authentication headers.
* Audio bytes.
* Transcript text.
* Clipboard contents.
* Window document contents.
* User-dictated prompt.
* Full sensitive filesystem paths where avoidable.

Use bounded rolling files. Provide a user action to open the diagnostic directory and another to clear diagnostics.

## 20. Code quality

Configure:

* Nullable reference types enabled.
* Implicit usings where appropriate.
* Deterministic builds.
* Treat compiler warnings as errors for project code.
* Current Microsoft code-analysis rules at a reasonable severity.
* `.editorconfig`.
* Consistent formatting.
* Central package version management if multiple projects use shared packages.
* XML documentation for public APIs where it adds value.
* No dead code.
* No commented-out implementations.
* No placeholder methods that return fake success.
* No `TODO` for a required acceptance criterion.
* No generated abstraction layers without behavior.
* No giant service classes.
* No UI-thread blocking.
* No unmanaged resource leaks.
* No unbounded queues or buffers.

Names should express intent. Comments should explain constraints and reasoning, not restate syntax.

## 21. Testing strategy

Create meaningful automated tests.

### Unit tests

Cover at minimum:

* All valid and invalid dictation state transitions.
* Duplicate hotkey handling.
* Cancellation.
* Configuration validation.
* Secret precedence.
* Language/context construction.
* Audio-size calculation.
* Pending-recording retention.
* Retry classification.
* Retry timing bounds.
* Transcript newline normalization.
* Target-window policy.
* Clipboard concurrency decisions.
* Insertion fallback behavior.
* Redaction of sensitive values.
* Cleanup after success and cancellation.

### Integration tests

Cover:

* Azure request construction through a mocked HTTP handler or local test server.
* Authentication-header placement without snapshotting the real key.
* Multipart audio upload.
* Deployment-name handling.
* API-version handling when applicable.
* Success response parsing.
* Error response classification.
* Rate-limit handling.
* Timeout and cancellation.
* Corrupt or unsupported audio.
* Credential-store adapter where safely testable.
* Pending-recording persistence across process restarts.

### Live contract test

Provide an opt-in live test that:

* Runs only when explicitly enabled.
* Reads credentials exclusively from environment variables or secure configuration.
* Uploads a short, non-sensitive test clip.
* Verifies that the configured deployment returns a nonempty transcript.
* Never runs in ordinary CI.
* Never prints the key or full response content.
* Documents expected Azure consumption.

### Manual Windows test matrix

Create `docs/manual-test-plan.md` covering:

* Notepad.
* VS Code.
* Edge or Chrome text area.
* Windows Terminal.
* PowerShell.
* Slack or Teams if installed.
* Multiline input.
* Norwegian characters.
* English technical terms.
* Long silence.
* Rapid start/stop.
* Microphone disconnect.
* Network disconnect before stop.
* Network failure during upload.
* Invalid API key.
* Invalid deployment name.
* Clipboard contention.
* Target window closed during processing.
* Target window changed during recording.
* Elevated target application.
* Multiple monitors.
* DPI scaling.
* Windows sleep/resume.
* Application restart with pending audio.

Document observed limitations honestly.

## 22. Build, CI, and dependency hygiene

Provide commands for:

* Restore.
* Build.
* Test.
* Format verification.
* Publishing a self-contained Windows x64 build.
* Running the opt-in Azure contract test.
* Packaging or distributing the application.

Add a Windows CI workflow that:

* Restores dependencies.
* Builds Release.
* Runs automated tests.
* Performs format verification.
* Does not require Azure credentials.
* Does not execute the live contract test.
* Does not upload sensitive artifacts.

Check:

* Outdated packages.
* Known vulnerable packages.
* Package licenses.
* Reproducible clean build.

Avoid dependencies with unclear maintenance or unsuitable licensing.

For the initial private release, a self-contained Windows x64 publish directory or archive is acceptable. Document code-signing and installer options, but do not fabricate a trusted certificate. If implementing MSIX or another installer, ensure it does not create unnecessary elevation requirements.

## 23. Documentation

Write a complete `README.md` containing:

* What the application does.
* Supported Windows version.
* Architecture summary.
* Prerequisites.
* Microsoft Foundry/Azure OpenAI deployment requirements.
* How to configure endpoint, key, deployment name, and API version.
* How secure key storage works.
* How to build.
* How to run.
* How to test.
* How to publish.
* Default hotkey.
* How to change microphone.
* How to retry a failed transcription.
* Where pending audio is stored.
* How to delete stored data.
* Known Windows limitations.
* Elevated-window limitation.
* Azure request-size limitation.
* Troubleshooting
