# Manual Test Plan

## Core Journey

1. Start VoiceType and configure endpoint, deployment, and API key.
2. Focus a non-sensitive text field.
3. Press `Ctrl+Alt+F9`, speak Norwegian with an English technical term, then press the shortcut again.
4. Confirm recording/processing states remain responsive.
5. Insert and verify Unicode, punctuation, multiline text, and that Enter was not added.

## Compatibility Matrix

Test Notepad, Visual Studio Code, a Chromium browser text area, Windows Terminal, and one WPF/Win32 text control. For each target, record original-target restoration, clipboard preservation, insertion result, focus behavior, and DPI/monitor arrangement.

## Failure Checks

- Missing/removed microphone and device change
- Invalid key, forbidden deployment, missing deployment, 429, 5xx, timeout, and disconnected network
- Target closed or elevated before insertion
- Clipboard held open by another process
- Repeated hotkey press and second app launch
- Sleep/resume and shutdown during recording
- Failed-audio discovery and cleanup

Never use sensitive speech or clipboard content during compatibility testing.
