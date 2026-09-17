# Build Ledger

Successful vertical slices are recorded here after two fresh-launch validations.

## Slice A: Controllable WPF shell

Requirements loaded: `_init/_init_01.md` sections 3, 4, 8, 9, 14; `_init/_init_02.md` sections 10, 11; `_init/_init-prompt.md` sections 4, 7, 16.

Capability implemented: .NET 10 WPF shell with Norwegian UI, stable UI Automation identifiers, accessible names, one command-owned action, and a FlaUI UIA3 executable driver with PID-scoped lifecycle and exact-window evidence capture.

How it was exercised: Built `TalkToMe.sln`, launched `TalkToMe.App.exe` through `TalkToMe.UiDriver`, found `MainWindow` by process and AutomationId, invoked `StartRecordingButton`, and read `ConnectionStatusText` through UIA.

Observed result: Two fresh accepted runs returned `Kontroll bekreftet`, captured an unclipped 520x360 window, and exited cleanly.

Evidence: `artifacts/validation/slice-a/run-5/` and `artifacts/validation/slice-a/run-6/`.

Known limitation: This slice proves desktop control only; the action does not record audio yet. The workspace is not currently a Git repository, so no local commit checkpoint is possible.

Next slice: Deterministic file-backed audio through the same recording state and accumulator intended for microphone frames.

## Slice B: Simulated audio-input path

Requirements loaded: `_init/_init_01.md` section 5.4; `_init/_init_02.md` sections 6.4 and 7; `_init/_init-prompt.md` sections 9 and 10.

Capability implemented: Shared `IAudioSource` frame contract; NAudio file-backed and WASAPI implementations; 16 kHz/16-bit/mono conversion; real-time/accelerated pacing; cancellation; explicit state transitions; incremental WAV recording; duration and level UI; command-line-only diagnostic activation.

How it was exercised: Launched the real app with the Norwegian MP3 fixture and diagnostic output flags, invoked start through UIA, observed source/status/duration and button states, stopped through UIA after more than two seconds, validated the WAV header, and closed the owned process.

Observed result: Two fresh accepted runs produced valid `pcm_s16le`, 16000 Hz, mono WAV files with 2.1-2.2 seconds of audio and returned to `Lydopptak klart`.

Evidence: `artifacts/validation/slice-b/run-3/` and `artifacts/validation/slice-b/run-4/`.

Known limitation: The WASAPI implementation compiles behind the same frame boundary but has not been exercised because unattended validation deliberately uses the deterministic fixture. WAV size monitoring and long-recording compression remain later hardening work.

Next slice: Submit the finalized diagnostic recording to the configured Azure/Foundry deployment and display a plausible transcript through UIA.

## Slice D: Target-preserving external insertion mechanics

Requirements loaded: `_init/_init_01.md` sections 5.2, 5.3, 5.7; `_init/_init_02.md` sections 6.3 and 6.7; `_init/_init-prompt.md` sections 13-15.

Capability implemented: `RegisterHotKey` global toggle (`Ctrl+Alt+F9`), original foreground HWND/PID/title capture, exact-target validation/reactivation, injectable clipboard and keyboard adapters, bounded clipboard retries, Unicode Ctrl+V insertion without Enter, conditional clipboard restoration, and a safe exact-PID WPF text target.

How it was exercised: Focused `TargetEditor`, started and stopped via the global hotkey, paced the Norwegian fixture through normal recording, invoked the diagnostic provider only after WAV finalization, clicked `InsertButton`, and read the external editor text through UIA.

Observed result: Two fresh runs inserted exactly 77 characters, preserved Norwegian text, added no Enter, captured both windows, and closed only owned app/target processes.

Evidence: `artifacts/validation/slice-d/run-6/` and `artifacts/validation/slice-d/run-7/`.

Known limitation: The transcription provider was diagnostic because live Azure is externally blocked as recorded in `docs/decisions.md`. Windows 11 Notepad reused an already-running user process, so unattended validation switched to the isolated WPF Edit target rather than risk unrelated user content. UIPI/elevated targets remain unsupported by design.

Next slice: Product lifecycle, settings/credential storage, recovery, diagnostics, and packaging; rerun insertion with live Azure and a separately owned Notepad when external prerequisites are available.

## Slice E: Protected settings and credential handling

Requirements loaded: `_init/_init_01.md` sections 5.9 and 6; `_init/_init_02.md` sections 6.9 and 9; `_init/_init-prompt.md` sections 16-18.

Capability implemented: Atomic per-user JSON settings, current-user DPAPI secret store, environment overrides, masked key replacement/status/removal UI, HTTPS/deployment validation, diagnostic storage isolation, and verified one-time key-file migration mode.

How it was exercised: Opened the real owned settings window through UIA, rejected HTTP, saved valid non-secret settings, wrote a generated synthetic key through the masked field, scanned ciphertext for plaintext, removed the key, and repeated from a fresh process.

Observed result: Both UI runs passed; storage tests passed; the real one-line repository key was migrated to `%LOCALAPPDATA%\\TalkToMe\\credential.bin`, verified by decrypt-and-compare without printing, and the plaintext source was deleted.

Evidence: `artifacts/validation/slice-e/run-2/` and `artifacts/validation/slice-e/run-3/`.

Known limitation: Connection testing, microphone selection, and actual start-with-Windows registration remain future settings work. Azure endpoint/deployment are still unavailable locally.

Next slice: Complete lifecycle/recovery documentation, Windows CI, and a self-contained Release package smoke test.

## Slice F: Application lifecycle and successful cleanup

Requirements loaded: `_init/_init_01.md` sections 5.1, 7, 12-13; `_init/_init_02.md` sections 6.1, 8, 15-16, 20-21.

Capability implemented: Tray menu, per-user single-instance mutex/activation event, startup-to-tray when configured, copy-last-transcript action, deterministic disposal, and successful-audio deletion.

How it was exercised: Rebuilt and reran the global-hotkey insertion journey while asserting that the finalized WAV no longer exists after successful insertion.

Observed result: Journey passed and successful audio was deleted. Tray UI and second-instance activation are implemented but not fully desktop-automated.

Evidence: `artifacts/validation/slice-f/run-1/`.

Known limitation: Tray interaction and second-instance activation need a dedicated shell-notification-area driver.

Next slice: Recovery actions and release regeneration.

## Slice G: Recovery actions and final layout

Requirements loaded: indexed reliability/recovery sections and final definition-of-done sections.

Capability implemented: Cancel recording with deletion, retry retained finalized audio, explicit pending-audio deletion, matching tray actions, implementation ADRs, status matrix, and formatting gate.

How it was exercised: Built, formatted, and reran the complete audio/transcription/insertion journey after recovery-state changes; inspected final UIA bounds and PNG.

Observed result: Main journey passes; primary/recovery rows render without overlap; `dotnet format --verify-no-changes` passes.

Evidence: `artifacts/validation/slice-g/final-layout/`.

Known limitation: Failure injection for retry/cancel UI and crash-recovery metadata are not yet covered by the functional driver.

Next slice: Regenerate Release package and run final packaged smoke/secret scan.

## Slice H: Self-contained Release checkpoint

Requirements loaded: indexed packaging, documentation, definition-of-done, and handover sections.

Capability implemented: Windows CI, self-contained `win-x64` publish, protected-key workspace/package scanner, implementation ADRs, requirements status, and release runbooks.

How it was exercised: Release build, four focused tests, format verification, clean publish, protected-key scans, two fresh packaged global-hotkey insertion journeys, and exact-PID second-instance probe.

Observed result: All gates passed. Each package journey produced a valid 2.0-second recording, displayed the transcript, inserted exactly 77 characters without Enter, deleted successful audio, and closed owned processes. The second app instance exited while the primary remained running.

Evidence: `artifacts/validation/release-final/run-1/` and `artifacts/validation/release-final/run-2/`.

Known limitation: This is a verified release checkpoint, not full specification completion. Live Azure, physical microphone/device behavior, packaged Notepad/VS Code compatibility, non-activating overlay, crash metadata/retention, connection test, microphone/hotkey settings, startup registration, and automated tray interaction remain as classified in `docs/requirements-status.md`.

Next slice: Resolve Azure endpoint/deployment and interactive Azure sign-in, run the live transcription journey, then close the remaining platform/product gaps in `docs/requirements-status.md`.

## Slice I: Provider adapters and offline default

Capability implemented: Stable provider IDs/descriptors, central registry/factory, serialized lifecycle coordinator, deterministic Azure migration/new-install Local Whisper selection, namespaced DPAPI secrets, conditional tabbed settings, provider testing, model repair, endpoint security, Local Whisper CPU adapter, preserved Azure adapter, and honest LM Studio/Ollama profiles.

How it was exercised: Verified current primary API documentation; inspected installed LM Studio and Ollama; called running Ollama 0.31.1 at `/api/version`; attempted installed LM Studio server startup and recorded it unreachable; verified the 190,085,487-byte model SHA-256; sent the real Norwegian MP3 through production recording and Local Whisper; exercised both server profiles through the real Settings UI; visually inspected all resulting screenshots.

Observed result: Debug build and 18 tests pass. Local Whisper produced a 243-character Norwegian transcript with multiple stable semantic anchors after 24 seconds of production audio. Ollama was reachable and explicitly non-STT; LM Studio was installed but its server remained unreachable and the UI still stated the documented non-STT limitation. No audio was sent to either text server.

Evidence: `artifacts/validation/local-whisper-pass-5/`, `artifacts/validation/settings-providers-2/`, and `artifacts/validation/server-capabilities/`.

Packaging result after the download-on-first-run pivot: Release build and 18 tests passed. Neither self-contained publish nor installed application contains `ggml-small-q5_1.bin`. Inno Setup 6.7.3 produced a 55,960,288-byte installer with SHA-256 `f6daa1fbb30223e3f721d7b688f74bc272fefb6839f921c8a2690a049f885924`; package secret scan passed. The exact isolated installed Release detected the absent model, displayed consent and progress UI, downloaded and verified 190,085,487 bytes, became ready, and then completed the 24-second Norwegian production transcription journey.

First-run evidence: `artifacts/validation/installed-first-run-model-final/`. Post-setup transcription evidence: `artifacts/validation/installed-first-run-transcription/`.

Known limitation: The first automated Local Whisper insertion reached a real transcript but the existing target activation step failed and left text on the clipboard; transcript-only production journeys pass in Debug and installed Release. First-run setup requires internet access to the pinned Hugging Face URL. No Azure credential/deployment exists locally.

## Slice J: Configurable transcription language

Capability implemented: persisted **Auto**, **Norwegian**, and **Norwegian + English** language modes shared by Local Whisper and Azure. Norwegian modes send the single supported `no` language hint to prevent short Norwegian speech from being misdetected as Swedish or Chinese. The bilingual mode additionally contributes prompt context for English speech and technical terms while preserving user vocabulary. Legacy, null, and unknown settings normalize to Norwegian.

How it was exercised: the real Settings UI journey verified the exact choices, legacy-null normalization, selection, and persisted `norwegian-english` value. The production recording path sent the 24-second Norwegian fixture through Local Whisper with the new default language path. Both journeys were repeated against locally installed self-contained Release 1.0.13 after the installer workflow terminated and verified all existing app processes.

Observed result: Debug and installed Settings journeys passed with unclipped selector UI and protected-storage checks. Debug and installed Local Whisper journeys each produced a 216-character transcript containing four expected semantic anchors. Silent installation left no application process running until validation explicitly launched it.

Evidence: `artifacts/validation/language-settings-final-20260917-102002/`, `artifacts/validation/language-local-whisper-20260917-102014/`, `artifacts/validation/installed-language-settings-20260917-103801/`, `artifacts/validation/installed-language-local-whisper-20260917-103811/`, and `artifacts/validation/language-install-1.0.13-final.log`.

Known limitation: the mixed-language mode is represented by a Norwegian language hint plus prompt context because neither current transcription provider accepts a two-language allowlist. Live Azure validation remains blocked without a configured Azure transcription deployment and credential.
