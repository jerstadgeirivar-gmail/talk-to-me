# Manual Test Plan

## Core Journey

1. Install TalkToMe per-user on a machine without Python, Azure variables, a separate AI server, or an existing model.
2. Confirm **Local Whisper** is selected and the application offers the one-time 181 MB model download. Exercise decline, retry, progress, cancellation, checksum failure, and successful setup.
3. Focus a non-sensitive text field, dictate Norwegian, and stop.
4. Confirm recording/model loading/transcription remain responsive and the transcript is non-empty and semantically accurate.
5. Insert and verify Unicode, punctuation, multiline text, and that Enter was not added.
6. Restart and confirm Local Whisper remains selected. Switch providers, save, and confirm the next transcription uses exactly that provider without fallback.

Use `_init/norwegian-audio-speech-test.mp3` for repeatable automation through the production recording and adapter paths. For each claimed provider record executable/service, version, endpoint, model, observed transcript, latency, screenshot, and insertion result.

## Compatibility Matrix

Test Notepad, Visual Studio Code, a Chromium browser text area, Windows Terminal, and one WPF/Win32 text control. For each target, record original-target restoration, clipboard preservation, insertion result, focus behavior, and DPI/monitor arrangement.

## Failure Checks

- Missing/removed microphone and device change
- Invalid key, forbidden deployment, missing deployment, 429, 5xx, timeout, and disconnected network
- Local model missing/wrong size/wrong checksum, repair cancellation, low memory, and unsupported native runtime
- LM Studio stopped/running and Ollama stopped/running; verify both report lack of documented speech-to-text and never send audio
- Unknown saved provider ID and incomplete explicitly selected Azure; verify no automatic Local fallback
- Target closed or elevated before insertion
- Clipboard held open by another process
- Repeated hotkey press and second app launch
- Sleep/resume and shutdown during recording
- Failed-audio discovery and cleanup

Never use sensitive speech or clipboard content during compatibility testing.

## Packaging journey

Run restore, Debug/Release build, tests, self-contained `win-x64` publish, Inno Setup compile, installer SHA-256, isolated per-user install, and package secret scan. Assert that neither publish nor installer contains `ggml-small-q5_1.bin`. Remove Azure variables for the default journey. Complete first-run setup, confirm the downloaded model hash before live transcription, then verify preservation on upgrade and intentional preservation after uninstall.
