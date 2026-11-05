# Transcription Configuration

## Selection precedence

1. A saved `transcriptionProviderId` is used exactly as selected.
2. A legacy installation with no ID, complete Azure endpoint/deployment, and protected Azure key is migrated once to `azure-openai`.
3. Every other new configuration is saved as `local-whisper`.

There is no automatic fallback. Saving Settings selects the provider and endpoint for the next transcription without waiting for an active job; that job keeps its original provider and endpoint limits.

## Registered transcription endpoints

**Settings > Transcription** supports up to 32 named endpoint profiles. Add creates a blank profile; Duplicate copies configuration and limits but never copies its credential. Remove is available when another profile remains, and its protected credential is removed when Settings are saved. Existing Azure configuration migrates to an **Existing Azure endpoint** profile using the original DPAPI key, without requiring the key to be entered again.

Each profile stores its base endpoint, deployment/model name, Azure API version, route/authentication mode, and segment-planning limits. Standard Azure mode appends the deployment transcription route; API version `v1` selects Azure's v1 route. **OpenAI-compatible route** appends `audio/transcriptions` to the configured base URL, so include the API prefix, such as `/v1/`, in that base URL. **Bearer token authentication** uses an Authorization bearer header instead of Azure's `api-key` header. Remote endpoints must use HTTPS; loopback HTTP is allowed. Route and authentication choices are independent and are not inferred from model names.

| Limit | Default | Purpose |
| --- | --- | --- |
| Maximum output tokens | 2,000 | Segment-planning budget for the selected deployment; not sent as an unsupported API parameter |
| Estimated tokens / minute | 600 | Conservative estimate of transcript density, configurable for language and speech |
| Maximum segment seconds | 300 | Independent duration ceiling |
| Maximum upload bytes | 24,000,000 | Per-file budget, configurable up to 25,000,000 with header headroom |
| Request timeout seconds | 600 | Per-request timeout, not a whole-job or audio-duration limit |

The initial duration ceiling is the minimum of the configured duration, `0.75 * maximum output tokens / estimated tokens per minute * 60`, and the upload budget expressed as 16 kHz/16-bit mono PCM. The 25% token margin is a planning heuristic, not proof that a response cannot hit its model limit. The defaults yield about 150 seconds per segment. Configure limits from the actual deployment contract; a model name containing or omitting `mini` does not establish its capacity. There is no hardcoded model catalog and no guaranteed increase in output capacity from switching to a similarly named model.

If a provider returns an output-token count at or above the configured budget, or a recognized context/audio-length error, TalkToMe retries smaller sample-aligned portions. Providers without token-usage metadata still use the planning limits, but silent truncation cannot be detected reliably from nonempty text alone. Model context/prompt limits remain independent; keep vocabulary and preceding-text guidance concise.

## Long audio and restart recovery

Long audio is split into ordered WAV segments rather than squeezed into one MP3. Native 16 kHz/16-bit mono recordings retain their PCM samples; other mono/stereo formats are decoded and normalized to that format without another lossy codec. Short files that already fit both duration and byte budgets are uploaded unchanged. Multichannel audio is rejected explicitly.

The splitter searches the final ten seconds of each full segment for a quiet boundary. Continuous speech uses a one-second overlap; ordered text assembly reconciles matching boundary phrases conservatively. This is not a guarantee that every boundary word is recognized correctly. Segment number and audio position remain visible while sending and waiting for a response. Cancel stops active transcription, and imported sources are always preserved. Recorded-audio retention continues to follow **Keep audio after failed transcription**.

**Keep encrypted progress for restart** is opt-in for each profile. Completed segment text and request IDs are stored in current-user DPAPI-encrypted checkpoints under `%LOCALAPPDATA%\TalkToMe\Diagnostics\TranscriptionJobs\<profile ID>`. Re-importing the same unchanged file with the same profile configuration, language and prompt resumes completed segments, avoiding their API calls. Changing the source, profile configuration or prompt starts a separate job. Failed/cancelled jobs retain progress only when opted in; success deletes the checkpoint and temporary segment audio. Checkpoints expire after 14 days when the endpoint next runs. **Clear saved progress** deletes that profile's stored checkpoints; it does not delete source audio. Do not clear progress during an active job, which can write another checkpoint as segments complete.

DPAPI protection does not isolate checkpoints from other software running as the same Windows user. Runtime logs remain transcript-, vocabulary-, prompt- and key-free. Endpoint credentials are separate current-user DPAPI secrets for each profile, and switching profiles clears an unsaved key field rather than sending it to another endpoint.

## Transcription language

**Settings → Dictation → Transcription language** controls the hint sent with normal dictation and retries:

| Choice | Provider language | Additional context |
| --- | --- | --- |
| Auto | Automatic detection | None |
| Norwegian | `no` | None |
| Norwegian + English | `no` | Speech is primarily Norwegian but may contain English words, technical terms, identifiers, and sentences |

Norwegian is the default, including for settings created by earlier versions. It avoids short Norwegian dictation being misdetected as Swedish, Chinese, or another language. Current Local Whisper and Azure transcription APIs accept only one language hint, not a language allowlist; therefore **Norwegian + English** keeps the Norwegian hint and supplies bilingual context through the prompt. Technical vocabulary entered by the user is appended to that context rather than replaced.

## Personal vocabulary and prompting

**Settings > Dictation > Edit vocabulary** opens `%LOCALAPPDATA%\TalkToMe\vocabulary.txt` in Notepad. This is a separate UTF-8 plain-text file outside the Git repository; put terms or short context on separate lines and save normally. No JSON escaping is needed. The coordinator reads the file for each transcription or retry, combines its contents with the selected language context, and passes it to Azure's multipart `prompt` field or Local Whisper's `WithPrompt`. No restart or settings save is required after editing the file. This is per-request guidance, not training or permanent model learning. There is currently no application-level token counter or prompt-length validation.

The file is created empty on first use. If an older settings file contains `TechnicalVocabulary` and the vocabulary file does not exist, the app writes that text to the file before clearing the legacy JSON value. An existing vocabulary file is authoritative, including when empty; saving settings never overwrites it. Deleting the file recreates it empty after migration. File-read/access failures and invalid UTF-8 report a transcription error rather than silently dropping hints. Fix the file and retry (or re-import the original audio). Diagnostic runs keep their vocabulary beside their isolated settings, not in the user's normal data folder. The installer does not package or overwrite this personal file.

Research checked on 2026-11-01:

- **Local Whisper:** budget approximately 224 tokens for prompt history, not pages of vocabulary. The upstream whisper.cpp implementation caps history at half the model's 448-token text context; special markers and runtime version can affect the exact usable count. Oversized prompts can lose earlier terms. The exact packaged runtime boundary has not been live-tested.
- **Azure Whisper:** OpenAI documents a 224-token prompt limit for `whisper-1`; do not apply that limit to every Azure transcription model.
- **GPT-4o Mini Transcribe:** supports text prompts. OpenAI publishes a 16,000-token context window and 2,000 maximum output tokens. The context window is not a guaranteed 16,000-token vocabulary allowance: audio and other request context also matter, and Azure API/deployment restrictions must be verified separately. The references checked do not establish an exact prompt-only ceiling for this application's Azure deployment.
- Tokens are not words or characters. Norwegian compounds, names, identifiers, and punctuation can consume multiple tokens. The built-in Norwegian + English context also consumes prompt budget.

Start with a short list of relevant correct spellings and a little domain context. A suggested initial GPT-model vocabulary budget is 500-1,000 tokens, an evaluation target rather than a provider limit. Compare the same real recordings with and without hints, checking both recognition of listed terms and accidental insertion of words that were not spoken. More vocabulary is not automatically more accurate.

Domain profiles and automatic relevant-term selection are **not implemented**. Large dictionaries should use relevant subsets rather than sending every entry. A separate correction step could handle common errors, but must preserve the original transcript because corrections can change meaning. Fine-tuning is a different workflow requiring suitable training data and a supported model; adding a vocabulary file does not fine-tune the model.

Vocabulary stays on this computer with Local Whisper. When Azure is selected, the supplied vocabulary is sent to the configured Azure resource along with the audio. Git exclusion does not prevent this transmission.

Sources: [OpenAI transcription prompting](https://platform.openai.com/docs/guides/speech-to-text), [GPT-4o Mini Transcribe model limits](https://developers.openai.com/api/docs/models/gpt-4o-mini-transcribe), [Azure transcription REST API](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/reference), and [upstream whisper.cpp prompt handling](https://github.com/ggml-org/whisper.cpp/blob/master/src/whisper.cpp). OpenAI's model page currently marks GPT-4o Mini Transcribe deprecated; Azure availability and retirement dates must be checked independently before changing deployments.

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
