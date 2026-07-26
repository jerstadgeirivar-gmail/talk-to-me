# Troubleshooting

## Azure setup missing

Open **Innstillinger** and provide an HTTPS Azure endpoint and exact deployment name. Save or replace the API key. Restart VoiceType after Azure setting changes.

## Hotkey unavailable

Another application has registered `Ctrl+Alt+F9`. Close the conflicting application. Hotkey customization is not yet implemented.

## Text was not inserted

Confirm the original target still exists and is not elevated. The transcript remains visible in VoiceType and can be copied from the tray. Windows UIPI prevents a normal process from injecting into administrator windows.

## Recording or transcription failed

Pending recordings are stored under `%LOCALAPPDATA%\VoiceType\Pending`. Do not share them unless their content is known to be non-sensitive. Check endpoint/deployment spelling and network access before retrying.

## Windows 11 Notepad automation

Packaged Notepad can reuse an existing process/tab. The unattended driver uses the isolated `VoiceType.TestTarget` to avoid touching unrelated Notepad content. Close existing Notepad instances before a manual Notepad smoke test.
