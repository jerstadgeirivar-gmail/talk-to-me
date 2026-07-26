# Build a Production-Quality Windows Voice Dictation Application

You are the principal software engineer responsible for designing, implementing, testing, documenting, and packaging a complete Windows desktop application.

Do not merely propose code or provide examples. Work directly in the repository, create the solution and all necessary files, run the relevant commands, resolve failures, and leave the repository in a verified, runnable state.

Use sound engineering judgment. Ask for clarification only when an essential decision cannot safely be inferred or when credentials, permissions, or destructive actions require explicit user involvement. Otherwise, proceed autonomously.

## 1. Product objective

Build a polished, reliable Windows 11 desktop application that lets the user:

1. Place the text cursor in almost any Windows application.
2. Press a configurable global keyboard shortcut.
3. Speak naturally in Norwegian for as long as needed, including long silent pauses.
4. Press the shortcut again to stop.
5. Send the complete recording to an existing Microsoft Foundry/Azure OpenAI speech-to-text deployment.
6. Receive a high-quality Norwegian transcription.
7. Insert the final text at the original cursor position.
8. Recover the transcript and audio safely if transcription or insertion fails.

The application is intended for one person on their own Windows PC. It is primarily used to dictate long AI prompts in Norwegian, often containing:

* English technical terminology
* source-code identifiers
* filenames and paths
* product names
* CLI commands
* configuration keys
* version numbers
* acronyms
* occasional English sentences inside Norwegian speech

The experience should resemble ChatGPT voice dictation, but work in applications such as:

* Visual Studio Code
* Notepad
* browsers
* terminals
* GitHub Copilot Chat
* JetBrains IDEs
* Microsoft Office
* Slack or Teams
* other standard Windows text fields

The application must be clean and first-class. Do not implement it as an AutoHotkey script, browser wrapper, Electron application, local web server, or fragile proof of concept.

## 2. Existing Azure capability

The user already has:

* access to Microsoft Foundry/Azure OpenAI
* a deployed transcription model
* an Azure endpoint
* an API key
* a deployment name

The deployment may use either:

* `gpt-4o-transcribe`
* `gpt-4o-mini-transcribe`

Do not hard-code a model name. Azure calls must use the configured deployment name.

Treat endpoint shape, API version, SDK support, accepted audio formats, language parameter, response schema, size limit, and streaming support as facts that must be verified against the current official Microsoft documentation and the actual endpoint.

Do not assume that public OpenAI endpoint conventions and Azure OpenAI conventions are identical.

The application must support configuration through these environment variables during development:

```text
VOICETYPE_AZURE_ENDPOINT
VOICETYPE_AZURE_API_KEY
VOICETYPE_AZURE_DEPLOYMENT
VOICETYPE_AZURE_API_VERSION
```

`VOICETYPE_AZURE_API_VERSION` should be optional if the selected official SDK does not require it.

Never commit, print, log, serialize into repository files, or expose the API key.

For the installed desktop application, store the API key using Windows Credential Manager or another appropriate per-user Windows secret-storage mechanism. Plaintext JSON configuration is not acceptable for secrets.

## 3. Required technology direction

Use a native Windows-oriented stack:

* C#
* the current supported .NET LTS version; prefer .NET 10 if available and compatible
* WPF for the desktop/tray UI
* Windows App SDK only if a concrete requirement justifies it
* `Microsoft.Extensions.Hosting`
* built-in dependency injection
* strongly typed options and validation
* `CommunityToolkit.Mvvm` where it reduces boilerplate
* WASAPI-based microphone capture, preferably through the maintained NAudio library
* the official current Microsoft/OpenAI .NET SDK if it fully supports the deployed Azure audio-transcription API
* otherwise, a narrowly implemented typed `HttpClient` based on the official Azure REST contract
* Win32 APIs through explicit, isolated interop wrappers
* xUnit for automated tests

Do not add dependencies reflexively. Every dependency must have a clear purpose, be actively maintained, have an acceptable license, and not duplicate a platform capability that can be implemented safely with the standard library.

Use central package version management if it improves consistency.

## 4. Engineering principles

Apply these principles pragmatically:

* SOLID
* separation of concerns
* dependency inversion at external boundaries
* high cohesion and low coupling
* explicit state transitions
* secure-by-default configuration
* fail-safe behavior
* deterministic cleanup
* structured concurrency and correct cancellation
* nullable reference types
* immutable models where appropriate
* small, focused methods and types
* clear naming
* testability
* accessibility
* observability without recording sensitive content
* KISS
* YAGNI
* DRY, without forcing unrelated concepts into premature abstractions

Avoid both monolithic code-behind and unnecessary enterprise architecture. This is a focused desktop utility, not a distributed platform.

No service, repository, factory, mediator, event bus, or abstraction should exist unless it protects a real boundary or reduces concrete complexity.

## 5. Functional scope for version 1

### 5.1 System tray application

The application should normally run in the Windows notification area.

The tray menu must provide:

* Start dictation
* Stop dictation, when recording
* Cancel dictation
* Open settings
* Open the last transcript
* Retry the last failed transcription when recoverable audio exists
* Exit

Only one application instance may run per Windows user session. A second launch should activate or notify the existing instance.

### 5.2 Global shortcut

Implement a configurable system-wide toggle shortcut using the appropriate Win32 hotkey API.

Behavior:

* First press: begin recording.
* Second press: stop, transcribe, and insert.
* Escape while recording: cancel, subject to reasonable focus and hotkey limitations.
* Repeated or simultaneous shortcut events must not start overlapping operations.

Choose a sensible default shortcut that is unlikely to conflict with Windows, VS Code, browsers, or common accessibility features. Make it configurable and validate registration. If registration fails, show a useful error and allow the user to choose another shortcut.

Do not use an unrestricted low-level keyboard hook if `RegisterHotKey` is sufficient.

### 5.3 Target-window handling

At recording start:

* capture the foreground window handle
* capture enough non-sensitive metadata to detect whether the target still exists
* do not steal focus
* do not move the caret
* do not inspect or upload the contents of the target application

At insertion time:

* prefer the originally captured target
* validate that the window still exists
* restore focus only when necessary and allowed
* never paste into an unrelated window silently
* if the target cannot be safely resolved, place the transcript on the clipboard and show a clear recovery notification

Provide a setting that can later allow “insert into currently focused window,” but keep “original target” as the initial default.

### 5.4 Microphone recording

Capture the configured Windows microphone through WASAPI.

Requirements:

* default communications or default capture device selection
* user-selectable microphone in settings
* clear handling of device removal or invalidation
* mono audio
* an API-compatible sample rate and bit depth
* no automatic stop caused by silence
* arbitrarily long natural pauses while recording
* live duration indicator
* basic input-level indicator
* cancellation support
* correct disposal of audio resources
* prevention of concurrent capture sessions

Verify the Azure endpoint’s supported file formats and limits before choosing the recording format.

Prefer a format that provides:

* reliable Windows-native encoding
* good speech quality
* manageable upload size
* compatibility with the deployed transcription API
* low implementation risk

If uncompressed WAV would hit the request-size limit too quickly, use a supported compressed format through a reliable Windows encoder. Do not add FFmpeg unless there is a documented and unavoidable reason.

Implement size and duration monitoring. The user must receive a warning before an API file limit is reached.

If safe long-recording support requires segmentation:

* segment at suitable silence boundaries
* retain ordering
* preserve cross-segment context where supported
* avoid dropping or duplicating audio
* combine transcriptions deterministically
* test boundary behavior

However, prefer a supported compressed format over premature segmentation if that provides sufficient recording duration.

### 5.5 Transcription

Create a provider boundary such as:

```csharp
public interface ITranscriptionProvider
{
    Task<TranscriptionResult> TranscribeAsync(
        RecordedAudio audio,
        TranscriptionContext context,
        CancellationToken cancellationToken);
}
```

The Azure implementation must:

* validate configuration before recording begins
* use HTTPS only
* use the configured deployment name
* authenticate securely
* use a Norwegian language hint only if supported by the actual Azure contract
* use the correct documented Norwegian code; do not guess between `no`, `nb`, or `nb-NO`
* optionally provide a configurable technical vocabulary/context prompt if the endpoint supports it
* enforce sensible connection and overall timeouts
* distinguish authentication, authorization, quota, rate-limit, network, malformed-response, unsupported-format, file-size, cancellation, and server errors
* avoid logging audio or transcript content
* preserve recoverable audio after failure
* avoid unsafe automatic retries after an ambiguous request outcome
* respect `Retry-After` for explicitly retryable responses
* expose a manual retry path

If the endpoint supports response streaming reliably, it may update the status overlay with transcription progress. Do not insert partial text into the target application in version 1. Insert only the completed final transcript.

Do not add a second LLM “cleanup” call in version 1. First establish the quality of the transcription model itself.

### 5.6 Transcript handling

By default:

* preserve the transcription model’s wording
* normalize line endings to Windows conventions
* avoid destructive trimming
* preserve Unicode
* preserve punctuation returned by the model
* do not rewrite technical terms
* do not fabricate punctuation through simplistic regex rules
* do not run heuristic filler-word removal

Provide extension points for future modes such as:

* verbatim transcription
* polished prompt
* custom dictionary
* spoken punctuation commands

Do not implement these unless the core version is complete and all required tests pass.

### 5.7 Text insertion

Create an abstraction such as:

```csharp
public interface ITextInsertionService
{
    Task<TextInsertionResult> InsertAsync(
        WindowTarget target,
        string text,
        CancellationToken cancellationToken);
}
```

Use a robust Windows-compatible insertion strategy.

Clipboard-assisted paste is acceptable and will probably be the primary strategy, but it must be implemented carefully:

* place Unicode text on the clipboard
* simulate the appropriate paste command
* handle temporarily locked clipboard access with bounded retry
* preserve previous clipboard text when safe
* never overwrite clipboard content that the user changed concurrently
* restore prior clipboard text only if the clipboard still contains the temporary application-owned value
* avoid logging clipboard contents
* leave the transcript on the clipboard if insertion fails
* notify the user exactly what happened

Do not claim universal compatibility. Windows UIPI prevents a normal-integrity application from injecting input into a higher-integrity/elevated application.

Do not make the entire application run as administrator by default.

Document the elevated-window limitation. If support for elevated target applications is later required, design a narrowly scoped elevated broker rather than elevating the full process. The broker is out of scope for version 1.

Ensure insertion does not add an implicit Enter key or submit prompts automatically.

### 5.8 Status overlay

Implement a small, polished, non-activating overlay that does not steal keyboard focus.

It should communicate:

* recording
* elapsed duration
* microphone activity
* stopping
* uploading
* transcribing
* inserting
* success
* recoverable failure
* cancellation

The overlay should be concise and visually unobtrusive.

Requirements:

* compatible with common DPI scaling
* keyboard accessible where interactive
* high-contrast aware
* screen-reader-friendly notifications where practical
* no rapid flashing
* no focus theft
* correct behavior across multiple monitors

### 5.9 Settings

Provide a small settings window with:

* endpoint
* deployment name
* API version if required
* API-key status without displaying the full key
* replace/remove API key
* connection test
* microphone selection
* global shortcut
* original-target versus current-target preference
* start with Windows
* optional technical vocabulary/context
* local retention choice for failed audio
* diagnostic logging level

Validate settings before saving.

A connection test must use a minimal safe request or documented deployment-discovery mechanism. It should clearly distinguish:

* invalid endpoint
* invalid key
* unavailable deployment
* unsupported API version
* missing quota
* network failure

Do not send the user’s clipboard or unrelated content as part of a connection test.

## 6. Security and privacy requirements

Create a short threat model covering:

* API-key disclosure
* accidental transcript logging
* temporary audio persistence
* clipboard races
* insertion into the wrong window
* malicious or malformed endpoint configuration
* dependency compromise
* recovery-data retention
* elevated process boundaries

Implement at minimum:

* secrets outside source control
* `.gitignore` coverage
* Windows-protected secret storage
* HTTPS endpoint validation
* redacted structured logging
* per-user application-data storage
* restrictive file access appropriate for the current user
* deterministic deletion of successful temporary recordings
* configurable retention of failed recordings
* explicit “Delete recovery data” action
* no analytics or telemetry
* no background microphone capture while idle
* visible recording indication
* no transcript history unless deliberately enabled

Never include API keys in:

* exception messages
* logs
* test snapshots
* crash reports
* process arguments
* generated documentation
* screenshots
* sample configuration

## 7. Reliability requirements

Use an explicit application state model. At minimum:

```text
Idle
Starting
Recording
Stopping
Transcribing
ReadyToInsert
Inserting
Completed
Cancelled
RecoverableFailure
FatalFailure
```

Define allowed transitions. Invalid transitions must be rejected rather than silently producing inconsistent state.

The application must remain correct when:

* the hotkey is pressed repeatedly
* stop is requested during startup
* cancel occurs while recording
* the microphone disappears
* network connectivity is lost
* Azure returns 401, 403, 404, 413, 429, or 5xx
* transcription takes unusually long
* the target window closes
* the target becomes elevated
* the clipboard is locked
* the user changes the clipboard during insertion
* the application is closed while audio is recoverable
* Windows begins shutdown
* the previous process crashed
* a second application instance is launched

User speech must never be silently discarded after the user has requested transcription. If a failure occurs after recording, retain recoverable audio until:

* transcription succeeds
* the user deletes it
* a documented retention limit is reached

Use atomic file operations for recovery metadata where appropriate.

## 8. Suggested solution structure

Use this as a starting point, adjusting only when there is a concrete reason:

```text
VoiceType.sln

src/
  VoiceType.App/
    WPF UI
    tray integration
    composition root
    view models
    application lifecycle

  VoiceType.Core/
    domain models
    state machine
    interfaces
    application use cases
    validation abstractions

  VoiceType.Infrastructure/
    Azure transcription provider
    audio capture and encoding
    Windows hotkey integration
    window targeting
    clipboard and input insertion
    credential storage
    persistence
    logging

tests/
  VoiceType.Core.Tests/
  VoiceType.Infrastructure.Tests/
  VoiceType.App.Tests/

docs/
  architecture.md
  threat-model.md
  manual-test-plan.md
  troubleshooting.md
  adr/
```

Keep Windows interop isolated and wrapped behind testable interfaces.

The Core project must not depend on WPF, NAudio, Azure SDKs, Win32-specific implementations, or concrete persistence.

Do not create separate projects solely to satisfy a layering diagram. If a smaller project structure results in clearer code, document the rationale in an ADR.

## 9. Code quality requirements

Configure:

* nullable reference types
* implicit usings where appropriate
* deterministic builds
* reproducible package restore
* analyzers
* formatting rules through `.editorconfig`
* warnings as errors for owned code, except for explicitly documented external/generated issues
* XML documentation only for public or non-obvious contracts
* no blanket warning suppression
* no empty catch blocks
* no `async void` except legitimate UI event handlers
* no sync-over-async
* no fire-and-forget tasks without explicit supervision
* no global mutable service locator
* no business logic in WPF code-behind
* no direct use of `MessageBox` throughout application logic
* no raw P/Invoke scattered across the codebase
* no static mutable configuration
* no secrets in constants
* no unbounded retry loops
* no unbounded in-memory audio accumulation

Use `CancellationToken` consistently.

Use `IAsyncDisposable` where async cleanup is required.

Dispose microphone, stream, file, tray, hotkey, and network resources correctly.

Use UTC for persisted timestamps and localized time only for display.

## 10. Testing strategy

### 10.1 Unit tests

Provide deterministic tests for:

* state-machine transitions
* invalid transition rejection
* rapid hotkey events
* cancellation
* configuration validation
* endpoint validation
* transcript normalization
* target selection rules
* clipboard ownership/race logic
* recovery metadata
* temporary-file cleanup rules
* retention policy
* error classification
* retry decisions
* audio-size threshold calculations
* transcript combination if segmentation exists

### 10.2 Infrastructure tests

Use test doubles or local HTTP handlers to test:

* successful Azure response
* malformed response
* 401
* 403
* 404
* 413
* 429 with `Retry-After`
* 500/503
* timeout
* cancellation
* interrupted upload
* ambiguous connection termination
* sensitive-header redaction

Do not make ordinary automated tests depend on a live Azure account.

### 10.3 Optional live contract test

Create an opt-in integration test that runs only when the required `VOICETYPE_AZURE_*` environment variables are present.

The test must:

* send a short, non-sensitive test audio sample
* verify a non-empty transcription
* avoid printing the API key
* be clearly labeled as consuming Azure resources
* not run by default in CI
* clean up temporary files

### 10.4 Manual compatibility matrix

Create and execute, where the environment permits, a documented manual test plan for:

* Notepad
* VS Code editor
* VS Code Copilot Chat
* Edge or Chrome text area
* Windows Terminal
* at least one WPF or Win32 text control
* multiple monitors
* 100%, 125%, and 150% DPI where practical
* clipboard containing text
* clipboard changed during transcription
* closed target window
* elevated target limitation
* long silence
* microphone disconnection
* network failure
* very long recording
* Norwegian mixed with technical English

Record actual results in the manual test document. Do not mark unexecuted tests as passed.

## 11. Build and continuous integration

Provide scripts or documented commands for:

```text
restore
build
format verification
unit tests
integration tests
publish
```

Add a Windows GitHub Actions workflow that:

* restores dependencies
* builds Release
* runs formatting verification
* runs non-live automated tests
* publishes test results
* does not require Azure credentials
* does not expose secrets

Run package vulnerability checks and document any findings.

Review dependency licenses. Avoid dependencies with licensing terms unsuitable for a personal distributable desktop application.

## 12. Packaging

Produce a self-contained Windows x64 release artifact.

The initial packaging may be a self-contained published directory or ZIP if installer signing is unavailable, but structure the application so a proper installer can be added cleanly.

Document:

* prerequisites
* installation
* first-run configuration
* API-key setup
* shortcut configuration
* start-with-Windows behavior
* uninstall
* recovery-data location
* log location
* security and privacy behavior

Do not claim that an unsigned binary avoids Windows SmartScreen warnings.

If implementing MSIX, WiX, Velopack, or another installer technology, explain the choice and ensure it does not create disproportionate complexity.

## 13. Documentation deliverables

Create a complete `README.md` containing:

* what the application does
* supported Windows versions
* architecture summary
* screenshots only if they can be generated honestly
* local development setup
* configuration variables
* secure API-key setup
* Azure deployment requirements
* build commands
* test commands
* publish commands
* usage instructions
* known limitations
* troubleshooting
* privacy notes

Also create:

* `docs/architecture.md`
* `docs/threat-model.md`
* `docs/manual-test-plan.md`
* `docs/troubleshooting.md`
* ADRs for major choices, including:

  * WPF versus alternatives
  * audio format and encoding
  * Azure SDK versus typed REST client
  * clipboard-assisted insertion
  * recovery-data policy
  * deployment and packaging approach

Documentation must describe the implementation that actually exists, not an aspirational design.

## 14. User experience quality bar

The result must feel like a deliberate Windows utility, not a developer demo.

Ensure:

* consistent spacing and typography
* clear status language
* concise Norwegian-facing UI text
* no raw stack traces shown to the user
* actionable error messages
* no modal-dialog cascade
* safe default behavior
* responsive UI during recording and networking
* no focus theft
* no unexpected prompt submission
* recoverable transcripts
* visible recording state
* graceful shutdown

The initial user-facing language should be Norwegian Bokmål. Keep localization resources separate from logic so English can be added later.

## 15. Explicit non-goals for version 1

Do not implement these until the required version is complete, verified, and documented:

* wake-word detection
* continuous always-listening mode
* spoken start/stop commands
* automatic LLM rewriting
* text editing by voice
* real-time insertion of partial transcripts
* speaker diarization
* cloud transcript history
* user accounts
* remote backend
* database
* cross-platform support
* mobile application
* browser extension
* team features
* analytics
* auto-update infrastructure
* administrator-level input broker

Leave clean extension points only where justified. Do not build unused systems.

## 16. Implementation workflow

Follow this order.

### Phase 1: Inspect and verify

1. Inspect the repository and local development environment.
2. Check for repository-specific instructions.
3. Determine installed .NET and Windows SDK versions.
4. Verify the current official Microsoft documentation for:

   * the exact Azure endpoint type
   * model deployment requirements
   * supported API version
   * official .NET SDK support
   * authentication
   * multipart request shape
   * accepted audio formats
   * maximum request size
   * Norwegian language configuration
   * response and error schemas
5. Never infer these details solely from memory.

### Phase 2: Design

1. Write a concise implementation plan.
2. Define the state model and component boundaries.
3. Document important architectural decisions.
4. Identify security-sensitive flows.
5. Define acceptance tests before implementing the full UI.

Do not stop after the plan. Continue to implementation unless blocked.

### Phase 3: Vertical slice

Implement and verify a minimal end-to-end vertical slice:

1. record a short microphone sample
2. stop manually
3. transcribe through the configured Azure deployment
4. display the result in the application
5. insert it into Notepad
6. retain recoverable data on failure

Do not build all secondary UI before this vertical slice works.

### Phase 4: Harden

Add:

* tray lifecycle
* settings
* global hotkey
* target-window validation
* overlay
* credential storage
* recovery workflow
* error classification
* tests
* redacted logs
* packaging

### Phase 5: Verify

Run:

* restore
* build
* analyzers
* formatting check
* unit tests
* non-live integration tests
* optional live Azure contract test when credentials are available
* publish
* package inspection
* dependency vulnerability scan

Fix failures rather than documenting them away.

## 17. Definition of done

The work is complete only when:

* the solution builds cleanly from a fresh restore
* automated tests pass
* no secrets are committed
* the application records microphone audio
* long silence does not stop recording
* the configured Azure deployment returns a transcription
* Norwegian Unicode text is preserved
* the final text can be inserted into Notepad and VS Code
* the application never submits the prompt automatically
* failed transcriptions leave recoverable audio
* failed insertion leaves recoverable text
* tray and hotkey lifecycle work
* invalid configuration produces actionable feedback
* sensitive data is absent from logs
* a self-contained Release build is produced
* documentation matches actual behavior
* known Windows limitations are documented
* the repository has no unexplained failing tests, warnings, or TODO placeholders in required functionality

## 18. Final report

At completion, provide a concise report containing:

1. What was implemented.
2. The resulting architecture.
3. Important design decisions.
4. Exact build and run commands.
5. Exact configuration steps.
6. Tests executed and their results.
7. Live Azure test status.
8. Location of the published artifact.
9. Known limitations.
10. Any remaining optional enhancements.

Clearly distinguish:

* verified functionality
* functionality requiring execution on the user’s Windows machine
* optional future work

Do not claim success for tests that were not run.

## 19. Credentials and blocking behavior

If Azure credentials are not already available in the environment:

* complete all work that does not require them
* do not ask the user to paste the API key into chat
* instruct the user to set the environment variables securely
* provide the exact PowerShell commands using placeholders
* continue with mocked tests
* mark the live contract test as pending

Example placeholders:

```powershell
$env:VOICETYPE_AZURE_ENDPOINT = "<azure-endpoint>"
$env:VOICETYPE_AZURE_API_KEY = "<api-key>"
$env:VOICETYPE_AZURE_DEPLOYMENT = "<deployment-name>"
$env:VOICETYPE_AZURE_API_VERSION = "<api-version-if-required>"
```

Never replace placeholders with guessed values.

Begin by inspecting the repository and environment, then proceed through the workflow above. Build the actual application.
