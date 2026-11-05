# Troubleshooting

## App exits while installing an update

Installed Release builds check for updates automatically at startup and hourly. Installer diagnostics are stored under `%LOCALAPPDATA%\TalkToMe\Updates\<version>\install.log`.

If TalkToMe does not return after an update, check that `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\TalkToMe` points to the intended installation. Do not install validation packages into an `artifacts` directory under the same Windows user: the stable installer identity will redirect that user's startup and uninstall registration. Reinstall the desired package normally to repair the registration.

## Local model missing or corrupt

Accept the first-run download prompt, or open **Settings → Transcription**, select **Local Whisper**, and choose **Repair model**. The installer intentionally excludes the large model. Setup is explicit, cancellable, and accepted only when the pinned size and SHA-256 match before placement under `%LOCALAPPDATA%\TalkToMe\Models`.

If the runtime reports an unsupported CPU/native runtime, install current Windows updates and the Microsoft Visual C++ 2022 x64 runtime. Local transcription requires sufficient memory; close large applications before retrying.

## Azure setup missing

Select **Azure OpenAI**, provide an HTTPS endpoint, exact deployment, API version, and protected key. Changes apply to the next transcription; no restart is required.

## LM Studio or Ollama says speech-to-text is unavailable

This is expected with the APIs verified on 2026-07-31. A running server can be reachable and still not be transcription-capable. TalkToMe never sends audio to chat/generate endpoints. Select Local Whisper or Azure unless a future documented audio API is implemented.

## Hotkey unavailable

Another application may have registered the configured combination. Choose a different global hotkey in **Settings**, save, and restart TalkToMe.

## Text was not inserted

Confirm the original target still exists and is not elevated. The transcript remains visible in TalkToMe and can be copied from the tray. Windows UIPI prevents a normal process from injecting into administrator windows.

## Recording or transcription failed

Use the document icon beside **Settings** to open runtime diagnostics in `%LOCALAPPDATA%\TalkToMe\Diagnostics`. Logs are UTF-8 JSON Lines named `runtime-<UTC date>-<process ID>.jsonl`. They include application lifecycle, dictation status, provider, audio size/duration, native encoder bitrate, upload size, Azure HTTP status/error code/request ID, elapsed time, and exception types/stacks. Audio, transcripts, prompts, vocabulary contents, API keys, and raw Azure response bodies are not logged. Paths and operational metadata can still be sensitive; review logs before sharing. Each daily process log is capped at 10 MiB, and logs older than 14 days are removed on the next write. Setting `DiagnosticLoggingLevel` to `None` disables logging; a saved change applies immediately. Other levels currently retain the same diagnostic events.

If Settings transcribes its short test but long recordings fail, inspect `segmentation.started`, `segment.completed`, `segment.reducing`, `segment.retry`, and `azure.request.started`. Version 1.0.21 uses configured PCM segments, not whole-file MP3 compression. Check the selected endpoint's upload bytes, output-token budget, estimated tokens per minute, duration and timeout in Settings. A short readiness test does not establish that every duration or model limit is configured correctly. Lower estimated tokens per minute allows longer segments and is not a way to increase actual model capacity.

For cancellation or restart recovery, enable **Keep encrypted progress for restart** on the endpoint before starting the job. Re-import the unchanged source with the same endpoint configuration and prompt; `segment.resumed` identifies reused portions. If saved progress is unreadable, use **Clear saved progress** for that endpoint and retry. Changing a profile or prompt starts a different checkpoint rather than combining potentially incompatible results. If the provider supplies no output-token usage, conservative duration planning is important because incomplete text cannot reliably be detected from a successful HTTP response alone.

Pending recordings are stored under `%LOCALAPPDATA%\TalkToMe\Pending`. Failed transcriptions are deleted by default; enable **Keep audio after failed transcription** before recording only when retry support is required. Do not share retained recordings unless their content is known to be non-sensitive. The selected provider is never silently replaced; correct the exact model, capability, credential, endpoint, or network error before retrying.

Imported audio is always preserved, regardless of failed-recording retention. **Retry transcription** retries the file that failed to import, not an older pending recording; **Delete recording** is unavailable for externally owned imported files. A genuine Azure rejection shows its HTTP status, service error code when available, and request ID. Correct Settings and retry without restarting. Settings readiness verifies a real short transcription, not every file format, encoding capability, duration, or request-size boundary.

If TalkToMe or Windows stops during recording, restart TalkToMe. The newest interrupted WAV is repaired and shown as recovered audio. Choose **Retry transcription**, or **Delete audio** to discard it. After recovery, focus the desired text field and press the configured hotkey to insert a ready transcript if the original target is no longer valid.

## Windows 11 Notepad automation

Packaged Notepad can reuse an existing process/tab. The unattended driver uses the isolated `TalkToMe.TestTarget` to avoid touching unrelated Notepad content. Close existing Notepad instances before a manual Notepad smoke test.
