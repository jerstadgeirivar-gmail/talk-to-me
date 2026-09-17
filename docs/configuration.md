# Transcription Configuration

## Selection precedence

1. A saved `transcriptionProviderId` is used exactly as selected.
2. A legacy installation with no ID, complete Azure endpoint/deployment, and protected Azure key is migrated once to `azure-openai`.
3. Every other new configuration is saved as `local-whisper`.

There is no automatic fallback. Saving Settings changes the coordinator for the next transcription and waits if one is active.

## Transcription language

**Settings → Dictation → Transcription language** controls the hint sent with normal dictation and retries:

| Choice | Provider language | Additional context |
| --- | --- | --- |
| Auto | Automatic detection | None |
| Norwegian | `no` | None |
| Norwegian + English | `no` | Speech is primarily Norwegian but may contain English words, technical terms, identifiers, and sentences |

Norwegian is the default, including for settings created by earlier versions. It avoids short Norwegian dictation being misdetected as Swedish, Chinese, or another language. Current Local Whisper and Azure transcription APIs accept only one language hint, not a language allowlist; therefore **Norwegian + English** keeps the Norwegian hint and supplies bilingual context through the prompt. Technical vocabulary entered by the user is appended to that context rather than replaced.

## Providers and privacy

| Provider | Required configuration | Audio destination |
| --- | --- | --- |
| Local Whisper | Installed verified `small-q5_1` model | Remains on this computer |
| Azure OpenAI | HTTPS endpoint, deployment, API version, DPAPI key | Configured Azure resource |
| LM Studio | Base URL; optional protected token/model reserved | No audio sent because current API lacks STT |
| Ollama | Base URL; optional protected token/model reserved | No audio sent because current API lacks STT |

A loopback server may use HTTP. A non-loopback URL must use HTTPS because “local server” does not guarantee local audio handling when pointed at another host.

## Recording and clipboard privacy

Completed audio is deleted as soon as transcription succeeds. Audio from a failed transcription is also deleted by default. Enable **Keep audio after failed transcription** only when you want the main-window retry and delete controls; changing this setting applies immediately after saving.

Automatic text insertion temporarily stages the transcript on the Windows clipboard. TalkToMe restores the previous clipboard data when insertion succeeds and it still owns the staged text. It performs the same cleanup when target activation or pasting fails. Choosing **Copy transcript** is explicit and leaves the transcript on the clipboard.

An application or Windows interruption can leave a recoverable WAV file in `%LOCALAPPDATA%\TalkToMe\Pending`. On the next start, TalkToMe offers recovery or deletion.

## Environment variables

- `TALKTOME_AZURE_ENDPOINT`, `TALKTOME_AZURE_API_KEY`, `TALKTOME_AZURE_DEPLOYMENT`, `TALKTOME_AZURE_API_VERSION` override Azure fields after Azure has been selected; they never change provider selection.
- `TALKTOME_WHISPER_MODEL_PATH` overrides only the Local Whisper model file after Local Whisper has been selected.

## Local resource and lifecycle behavior

The installer does not contain the model. On the first Local Whisper start without a valid model, TalkToMe asks before downloading it. The pinned model is 190,085,487 bytes. Allow roughly 500 MB disk and 1.5 GB available memory; CPU speed controls latency. The model stays installed across upgrades and uninstall. First-run setup and **Repair model** download to a temporary file, report progress, support cancellation, verify size/SHA-256, and atomically replace the model only after validation.
