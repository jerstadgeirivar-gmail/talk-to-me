# Transcription Configuration

## Selection precedence

1. A saved `transcriptionProviderId` is used exactly as selected.
2. A legacy installation with no ID, complete Azure endpoint/deployment, and protected Azure key is migrated once to `azure-openai`.
3. Every other new configuration is saved as `local-whisper`.

There is no automatic fallback. Saving Settings changes the coordinator for the next transcription and waits if one is active.

## Providers and privacy

| Provider | Required configuration | Audio destination |
| --- | --- | --- |
| Local Whisper | Installed verified `small-q5_1` model | Remains on this computer |
| Azure OpenAI | HTTPS endpoint, deployment, API version, DPAPI key | Configured Azure resource |
| LM Studio | Base URL; optional protected token/model reserved | No audio sent because current API lacks STT |
| Ollama | Base URL; optional protected token/model reserved | No audio sent because current API lacks STT |

A loopback server may use HTTP. A non-loopback URL must use HTTPS because “local server” does not guarantee local audio handling when pointed at another host.

## Environment variables

- `TALKTOME_AZURE_ENDPOINT`, `TALKTOME_AZURE_API_KEY`, `TALKTOME_AZURE_DEPLOYMENT`, `TALKTOME_AZURE_API_VERSION` override Azure fields after Azure has been selected; they never change provider selection.
- `TALKTOME_WHISPER_MODEL_PATH` overrides only the Local Whisper model file after Local Whisper has been selected.

## Local resource and lifecycle behavior

The installer does not contain the model. On the first Local Whisper start without a valid model, TalkToMe asks before downloading it. The pinned model is 190,085,487 bytes. Allow roughly 500 MB disk and 1.5 GB available memory; CPU speed controls latency. The model stays installed across upgrades and uninstall. First-run setup and **Repair model** download to a temporary file, report progress, support cancellation, verify size/SHA-256, and atomically replace the model only after validation.
