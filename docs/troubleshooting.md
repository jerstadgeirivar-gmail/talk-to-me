# Troubleshooting

## Azure setup missing

Open **Innstillinger** and provide an HTTPS Azure endpoint and exact deployment name. Save or replace the API key. Restart TalkToMe after Azure setting changes.

## Hotkey unavailable

Another application may have registered the configured combination. Choose a different global hotkey in **Settings**, save, and restart TalkToMe.

## Text was not inserted

Confirm the original target still exists and is not elevated. The transcript remains visible in TalkToMe and can be copied from the tray. Windows UIPI prevents a normal process from injecting into administrator windows.

## Recording or transcription failed

Pending recordings are stored under `%LOCALAPPDATA%\TalkToMe\Pending`. Do not share them unless their content is known to be non-sensitive. Check endpoint/deployment spelling and network access before retrying.

If TalkToMe or Windows stops during recording, restart TalkToMe. The newest interrupted WAV is repaired and shown as recovered audio. Choose **Retry transcription**, or **Delete audio** to discard it. After recovery, focus the desired text field and press the configured hotkey to insert a ready transcript if the original target is no longer valid.

## Windows 11 Notepad automation

Packaged Notepad can reuse an existing process/tab. The unattended driver uses the isolated `TalkToMe.TestTarget` to avoid touching unrelated Notepad content. Close existing Notepad instances before a manual Notepad smoke test.
