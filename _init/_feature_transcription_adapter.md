One technical caveat is built into the prompt: current LM Studio documentation lists text, response, embedding, and model-management endpoints—not audio transcription—and Ollama likewise should not be assumed to support speech-to-text. The implementing agent must verify current official APIs and must not pretend an adapter can transcribe when the server lacks that capability. [LM Studio API](https://lmstudio.ai/docs/developer/openai-compat), [faster-whisper](https://github.com/SYSTRAN/faster-whisper).

Copy and give this prompt to the implementation agent:

```text
You are working in the TalkToMe repository, a Windows 11 WPF/.NET 10 dictation application.

Your assignment is to fully implement a selectable, extensible transcription-provider architecture using the Adapter pattern. Do not stop at a design proposal. Inspect the repository, update the design documentation, implement the change, build it, run it, test it through the UI, capture screenshots, visually inspect those screenshots, fix problems, and repeat until the result demonstrably works.

Product outcome
===============

A normal user must be able to:

1. Download the TalkToMe installer.
2. Install it on a Windows 11 x64 computer.
3. Start dictating and receive a transcription without:
   - an Azure account;
   - Python already installed;
   - manually installing a runtime;
   - manually downloading a speech model;
   - setting environment variables;
   - running a separate server.
4. Optionally configure Azure or a compatible locally hosted service later.
5. Select and test the transcription provider from a clean, expandable settings UI.

The application must ship with a local, CPU-compatible speech-to-text provider as the default. Use a suitable Whisper-family implementation such as faster-whisper, whisper.cpp/Whisper.net, or an equivalent mature implementation. Select the implementation that produces the most reliable self-contained Windows installation.

Do not require system Python. If faster-whisper is used, package a private pinned runtime and all required CPU dependencies. A native .NET/whisper.cpp solution is acceptable—and may be preferable—if it makes the installed product genuinely self-contained.

Repository context
==================

Inspect the repository before changing anything. Important existing areas include:

- `TalkToMe.Core/ITranscriptionProvider.cs`
- `TalkToMe.Core/ApplicationSettings.cs`
- `TalkToMe.Infrastructure/AzureTranscriptionProvider.cs`
- `TalkToMe.Infrastructure/AzureTranscriptionOptions.cs`
- `TalkToMe.Infrastructure/JsonApplicationSettingsStore.cs`
- `TalkToMe.Infrastructure/DpapiSecretStore.cs`
- `TalkToMe.App/App.xaml.cs`
- `TalkToMe.App/SettingsWindow.xaml`
- `TalkToMe.App/SettingsWindowViewModel.cs`
- `TalkToMe.App/MainWindowViewModel.cs`
- `packaging/TalkToMe.iss`
- `.github/workflows/windows-installer.yml`
- `tools/TalkToMe.UiDriver`
- `_init/norwegian-audio-speech-test.mp3`
- existing architecture, ADR, threat-model, troubleshooting, and test documentation.

The existing `ITranscriptionProvider` contract is the application-facing port. Preserve that clean boundary where practical. The current problem is that `App.xaml.cs` directly constructs Azure and the settings model/UI are Azure-specific.

Architecture requirements
=========================

Implement a maintainable provider registry/factory around `ITranscriptionProvider`.

The design should have concepts equivalent to:

- stable provider identifiers;
- provider descriptors/metadata for the UI;
- provider-specific configuration;
- provider-specific validation;
- provider health/capability testing;
- an `ITranscriptionProviderFactory`, registry, resolver, or equivalent;
- adapters that normalize provider results and failures into the existing core types.

Do not put a growing provider `switch` statement throughout `App.xaml.cs`, view models, or XAML code-behind. Centralize construction and validation.

Implement these provider adapters:

1. Local Whisper
   - The default for a new installation.
   - Performs speech-to-text entirely on the computer.
   - Must support Norwegian and the existing 16 kHz mono WAV recording flow.
   - CPU-only Windows systems must work.
   - Cancellation must work.
   - Normalize line endings and typed failures consistently.
   - Pass `TranscriptionContext.Language` and vocabulary/prompt where supported.
   - Keep the model loaded or cache it appropriately so repeated dictation does not incur unnecessary startup cost.
   - Do not download anything during each transcription.

2. Azure OpenAI
   - Refactor and preserve the existing Azure adapter and its tested REST behavior.
   - Preserve endpoint, deployment, API version, API key, timeout, request ID, error classification, environment overrides, and failed-audio behavior.

3. LM Studio
   - Create a named adapter/profile and configuration UI for an LM Studio server.
   - Verify the current official LM Studio API before implementing.
   - Do not assume that OpenAI-compatible chat support includes `/v1/audio/transcriptions`.
   - If current LM Studio supports audio transcription, implement the documented contract and test it.
   - If it does not, the adapter must detect that fact and display a clear “server does not expose speech-to-text” result. It must not send audio to a chat endpoint or claim success.
   - Allow a configurable base URL, model, optional API token, and transcription path only when that represents a real compatible service.

4. Ollama
   - Create a named adapter/profile and configuration UI for an Ollama server.
   - Verify the current official Ollama API and model capabilities first.
   - If Ollama has a documented audio-capable API/model, implement it.
   - If it does not, report the limitation honestly through capability testing. Never convert the audio to text by pretending a text-only model performed transcription.
   - Keep the adapter boundary ready for a future documented Ollama audio API.
   - Allow configurable base URL/model and optional authentication where meaningful.

LM Studio and Ollama may share a well-tested OpenAI-compatible HTTP transcription implementation internally when their actual server contracts permit it, but they must remain separate provider descriptors/adapters in the registry and UI.

Document the verified capabilities and limitations in a new ADR. Rely on current primary documentation rather than assumptions or old blog posts.

Provider selection and migration
================================

Use deterministic selection rules:

1. An explicit, valid provider selection made by the user wins.
2. For an existing installation with complete Azure configuration but no saved provider ID, migrate/select Azure so existing users do not unexpectedly change behavior.
3. For a fresh installation without a provider selection, use Local Whisper.
4. The Local Whisper choice for a fresh installation is a default, not a fallback.
5. Never switch providers automatically after a provider has been selected or inferred during migration.
6. If the selected provider is incomplete, unavailable, unknown, removed, or broken, stop the transcription and show the exact actionable error. Do not silently or automatically use Local Whisper, Azure, or any other provider instead.
7. A selected Azure provider with incomplete configuration is a configuration error. The presence of a working Local Whisper installation must not hide it.
8. Environment overrides must remain supported and documented. They must not unexpectedly override an explicit UI selection unless that precedence is deliberately specified and visible.

Do not implement provider fallback chains, catch-and-continue provider selection, or a “try the next provider” policy. Robustness here means deterministic behavior: the selected adapter works, or the operation fails clearly and completely. Defaults are allowed only when creating a genuinely new configuration.

Settings changes should apply to the next transcription without requiring a full application restart if this can be done safely. Dispose replaced providers correctly and never replace a provider during an active transcription. If a restart is genuinely required for a particular engine, state that clearly in the UI.

Local model installation
========================

Make successful installation leave TalkToMe ready to transcribe.

Choose and document a pinned multilingual model appropriate for Norwegian. Balance accuracy, CPU speed, disk size, memory use, installer size, and licensing. Do not silently choose an English-only model.

The installer/build must either:

- bundle the runtime and model in the installer; or
- download and verify the pinned model as an explicit installer step with progress, retry, cancellation, and a clear failure message.

A first-run manual download button is not sufficient for the default happy path. A repair/download button may still be provided.

Requirements:

- Verify downloaded assets with a pinned SHA-256 checksum.
- Pin runtime and model versions.
- Include required license and attribution files.
- Store models in an appropriate application data location.
- Avoid administrator privileges where possible.
- Updates should preserve an already valid model and avoid unnecessary downloads.
- Uninstallation behavior must be intentional and documented.
- Never execute unverified downloaded binaries.
- Do not depend on CUDA, although optional acceleration may be supported.
- Provide actionable errors for missing, corrupt, or incompatible local model files.
- Do not block the WPF UI thread while loading or transcribing.

Settings UI
===========

Redesign the settings window so it remains consistent with the existing TalkToMe visual language and can accommodate more providers.

A suitable design would use a `TabControl` with:

- “Transcription”
- “Dictation”
- optionally “Advanced”

The Transcription tab should include:

- a clearly labelled provider selector;
- concise descriptions such as “Works offline” and “Requires a running local server”;
- provider readiness/status badge;
- only the fields relevant to the selected provider;
- a “Test provider” action;
- local model status and repair/download action;
- progress and cancellation for slow tests/downloads;
- accessible labels and stable AutomationIds;
- validation close to the relevant field;
- no exposed saved secrets;
- scrolling and resizing that work at 100%, 125%, 150%, and 200% display scaling.

Suggested provider-specific fields:

- Local Whisper: model, execution mode when useful, installed/ready status.
- Azure: endpoint, deployment, API version, protected API key.
- LM Studio: base URL, model, optional protected token, capability result.
- Ollama: base URL, model, optional protected token, capability result.

Use sensible localhost defaults for locally hosted services, but do not mark them ready until tested.

Do not use one generic API-key slot for unrelated services. Extend protected storage to support namespaced provider secrets and migrate the existing Azure credential without exposing or logging it.

The UI must clearly distinguish:

- configured;
- reachable;
- transcription-capable;
- unavailable;
- local model missing/corrupt.

Security and privacy
====================

- Preserve DPAPI protection for secrets.
- Never write keys/tokens into `settings.json`, logs, screenshots, UI automation dumps, or test artifacts.
- Local Whisper audio must remain local.
- A local-server provider may still send audio outside the machine if configured with a non-loopback URL. Make that visible to the user.
- Require HTTPS for non-loopback remote endpoints unless an explicitly documented development override exists.
- Allow HTTP for loopback addresses such as localhost/127.0.0.1.
- Redact authorization headers and sensitive query parameters.
- Update the threat model.

Failure behavior
================

Map failures consistently into existing `TranscriptionFailureCategory` values, extending the enum only when justified.

Handle at least:

- local model missing or corrupt;
- unsupported CPU/native runtime;
- insufficient memory;
- local engine process crash, if a sidecar is used;
- connection refused;
- timeout and cancellation;
- authentication/authorization;
- unsupported endpoint;
- selected model missing;
- malformed response;
- HTTP 429 and 5xx;
- recordings above provider limits.

Retain retryable audio according to existing policy. Provider diagnostics must not delete recordings.

Never respond to a provider failure by invoking another provider. Preserve the original failure, provider identity, and useful diagnostic context so the problem can be found and fixed.

Tests
=====

The primary test strategy is live, full end-to-end testing. Tests must exercise the same installed application, adapters, network calls, model runtimes, audio files, UI, and output path that a real user exercises.

Do not add mocked HTTP handlers, fake servers, stub providers, simulated provider responses, diagnostic transcripts, in-memory substitutes, or tests that merely verify implementation details. Do not call a test end-to-end if the real transcription provider was replaced. Do not add a broad regression-test suite for its own sake.

Unit tests are permitted only for a substantial deterministic algorithm that is difficult to observe precisely through the live product. Examples might include a non-trivial audio transformation or checksum algorithm. Configuration wiring, adapter selection, DTO serialization, HTTP request construction, getters, setters, and view-model plumbing are not reasons to create unit tests. If no genuine algorithm needs a unit test, add no unit tests.

Use live-test-driven development:

1. Define or automate the real user journey first.
2. Run it against the real dependency and observe the genuine failure.
3. Implement the smallest coherent product change that makes the journey work.
4. Run the complete journey again.
5. Inspect the transcript, application state, target application, logs, and screenshots.
6. Continue until the real journey passes reliably.

Every shipped adapter must be tested with a live, running backend and a real audio file:

- Local Whisper: run the installed local runtime and pinned model.
- Azure: call the configured live Azure transcription deployment with the real protected credential.
- LM Studio: locate the installed LM Studio application/server, start it if it is stopped, load the required real model, and call its real endpoint.
- Ollama: locate the installed Ollama application/service, start it if it is stopped, ensure the required real model is present, and call its real endpoint.

Before asking the user for help with a local dependency, inspect the machine autonomously: installed applications, executable search paths, Windows services, running processes, standard data directories, configuration files, and standard localhost ports. Start an installed LM Studio or Ollama service yourself when that is safe and within this task. Do not ask the user merely to run a command that you can run.

If a required backend, model, endpoint, or credential genuinely does not exist after those checks, stop and report the exact missing prerequisite. For example, state that a live LM Studio speech-to-text server or a specific model must be installed/configured. Do not replace the missing service with a mock, do not skip the adapter while claiming completion, and do not use another provider as a fallback.

Verify current LM Studio and Ollama capabilities against both official documentation and the live installed versions. If either product does not expose a real audio-transcription capability, do not manufacture a passing adapter around a text/chat endpoint. Treat that as an explicit product/design blocker and report it. An adapter may only be declared working when a real audio file goes in and a real transcript comes back from that backend.

Use `_init/norwegian-audio-speech-test.mp3` as the standard live input where suitable. It is acceptable for automation to feed an audio file instead of physically speaking into a microphone, but the file must pass through the real production recording/transcription path and selected production adapter. Validate a non-empty Norwegian transcript and stable semantic content, while allowing reasonable punctuation differences between engines.

Exercise the real UI as a user would. Extend `TalkToMe.UiDriver` only as an automation driver for the production application; it must not inject provider results. It should:

- install or launch the real build;
- open Settings;
- select and configure each real provider;
- save the configuration;
- invoke the provider test through the UI;
- transcribe the real Norwegian audio fixture through that selected adapter;
- verify the transcript shown by TalkToMe;
- verify insertion into a real running target application;
- restart TalkToMe and verify the saved provider is still selected;
- verify secrets stay masked;
- capture screenshots for every provider and the completed transcription journey.

Test one adapter at a time with no fallback enabled. A failing adapter test must fail the run visibly and retain its evidence.

Build, run, inspect, fix
=======================

Perform the work autonomously. Preserve unrelated user changes.

At minimum run:

- `dotnet restore TalkToMe.sln`
- Debug build
- Release build
- every live end-to-end provider journey whose real prerequisites are available
- the settings UI-driver scenario against production providers
- the default Local Whisper installed-product journey
- a self-contained `win-x64` publish
- Inno Setup compilation
- installer/package integrity checks

Then install the produced installer into an isolated per-user test location or a clean Windows environment when available. Verify with no Azure variables, no configured Azure account, and no system Python dependency that:

1. TalkToMe starts.
2. Local Whisper is selected and ready.
3. The Norwegian fixture can be transcribed.
4. The transcript appears in the application.
5. Settings can switch providers.
6. Restarting preserves the selection.
7. Azure migration behavior remains correct.

After the default installed-product test, exercise Azure, LM Studio, and Ollama separately using their real services. Never let success from Local Whisper conceal failure in another selected adapter.

Capture screenshots into a clearly named validation artifact directory. Open and visually inspect every screenshot. Check clipping, overlapping, incorrect conditional fields, blank states, scaling, misleading statuses, and accidental secret exposure.

If a build, test, UI scenario, installer run, or screenshot inspection fails, diagnose it, fix it, and rerun the relevant checks. Do not declare success based only on compilation.

Documentation
=============

Update:

- README
- architecture documentation and runtime diagram
- a new provider-selection/adapter ADR
- packaging ADR
- configuration documentation
- troubleshooting
- manual test plan
- threat model
- build/validation ledger where the repository uses it

Document:

- which local engine/model/version is shipped;
- model size and expected CPU/RAM/disk requirements;
- where the model is stored;
- offline behavior;
- how model repair works;
- provider selection precedence;
- Azure configuration;
- LM Studio and Ollama capability requirements;
- every supported environment variable;
- privacy implications of local versus remote endpoints;
- installer behavior and licensing.

Definition of done
==================

The task is complete only when:

- a fresh installed copy transcribes without Azure or manual dependency setup;
- the Adapter pattern is visible and genuinely supports adding providers without editing unrelated UI/application code;
- Local Whisper, Azure, LM Studio, and Ollama provider entries and adapters exist;
- unsupported server capabilities are detected honestly;
- settings are clean, accessible, responsive, and expandable;
- existing Azure users migrate safely;
- secrets remain protected;
- every claimed provider has completed a live audio-to-transcript journey through the production application;
- the installed-product, UI, publish, and installer journeys pass without mocks, stubs, simulations, or provider fallback;
- screenshots have been captured and visually inspected;
- documentation matches actual behavior.

Final response
==============

Provide:

1. A concise summary of the implemented architecture.
2. The chosen local engine/model and why.
3. Exact files changed.
4. Exact commands run and their results.
5. Installer path, size, and checksum.
6. Screenshot/evidence paths.
7. Verified behavior for every provider.
8. Any genuine external limitation, especially LM Studio or Ollama audio support.
9. Remaining risks or follow-up work.

For each provider, name the actual executable/service, endpoint, model, audio fixture, observed transcript evidence, and live-test result. Clearly mark anything that could not be exercised as incomplete.

Do not claim a provider or installer works unless you actually exercised it end to end. Do not describe mocks, simulated responses, compilation, or request-shape inspection as proof that transcription works. Do not publish a release, push a branch, or modify unrelated external systems unless separately authorized; deliver the repository in a tested, release-ready state.
```
