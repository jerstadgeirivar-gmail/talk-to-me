# ADR 002: Audio Format

Use 16 kHz, 16-bit, mono PCM frames and incrementally finalized WAV files. This is natively supported, deterministic, accepted by Azure, and low risk.

## Current decision: bounded PCM segments

Version 1.0.21 replaces whole-file MP3 preparation with bounded, ordered WAV segments. Native 16 kHz/16-bit mono PCM is copied sample-aligned without lossy encoding. Other mono/stereo imports are decoded and normalized to this format; short inputs that already fit the selected profile's byte and duration budgets bypass segmentation. No FFmpeg dependency or silent provider fallback is introduced.

Named endpoint profiles control output-token planning, estimated transcript tokens per minute, maximum duration, upload bytes, request timeout, route and authentication. Limits come from configuration, not a model-name catalog. Duration planning reserves 25% of the configured output budget; actual output-token usage and recognized limit errors trigger smaller segments. A nonempty result is not proof of complete recognition when a provider omits usage/truncation metadata.

Quiet boundaries are preferred; continuous speech uses one-second overlap and conservative ordered text reconciliation. Preparation uses bounded PCM buffers and temporary segment files. Optional current-user DPAPI-encrypted checkpoints allow cancelled/interrupted jobs to resume completed portions. Checkpoints are keyed by source hash, configuration, language and prompt, cleaned up on success, and expire after 14 days. See [configuration](../configuration.md#long-audio-and-restart-recovery).

Real Debug validation completed an exact two-hour audiobook with 49 successful segments, two segments reused after restart, a nonempty assembled transcript, unchanged source SHA-256, no gaps in submitted audio coverage, and uploads below 4.8 MB. Dense shorter speech separately exercised output-budget reduction, cancellation and recovery. This verifies bounded processing and source coverage, not a word-error-rate or exhaustive boundary-word accuracy benchmark. Paid long-validation inputs are capped at two hours; the earlier seven-hour run was interrupted and must not be reported as completed.

## Historical compression recovery, version 1.0.20

Versions 1.0.15 through 1.0.20 used whole-file MP3 preparation above 24 MiB. Version 1.0.20 negotiated the lowest installed bitrate at or above 32 kbit/s; the verified Windows encoder started at 48 kbit/s for 32 kHz mono. This fixed a local encoding failure but did not scale to arbitrary recording lengths. That preparation path is now removed.

The retained 788.28-second WAV that failed in version 1.0.19 was 25,225,006 bytes. Native bitrate negotiation produced a 4,730,615-byte MP3 and the real configured Azure deployment transcribed it successfully. The failure was local encoder selection, not endpoint authentication or reachability; short Settings test audio did not exercise compression.

## Multi-hour assessment, 2026-11-05

This assessment describes version 1.0.20 before the segmented implementation above. `AzureAudioUploadPreparer` prepared one whole-file MP3 at a fixed bitrate, and `AzureTranscriptionProvider` sent one request after rejecting prepared audio above 25 MiB. It did not progressively reduce bitrate to force arbitrary recordings below the limit, split uploads, checkpoint partial transcripts, or resume completed segments. Its request timeout was ten minutes; this was a request timeout, not a supported audio-duration guarantee.

Five hours of the app's native 16 kHz/16-bit mono PCM is approximately 576,000,000 bytes before the WAV header. At the verified 48 kbit/s MP3 bitrate, five hours is approximately 108,000,000 bytes before container overhead, still far above the request limit. The application's 25 MiB size ceiling alone corresponds to approximately 72.8 minutes at that bitrate, not a promise that the model can transcribe that much speech in one request. Microsoft documents the service limit as 25 MB; a future segment size budget should leave headroom below 25,000,000 bytes rather than depend on the application's larger binary-unit interpretation.

MP3 encoding is lossy. Fixed-bitrate quality does not progressively worsen just because a file is longer. No original-versus-MP3 word-error-rate or transcript-completeness comparison was performed, so neither unchanged recognition quality nor a measured accuracy regression has been established. A nonempty transcript from the 13-minute recording does not certify full coverage of every spoken word.

The available Git history shows whole-file MP3 preparation introduced in commit `0d599ef` (version `1.0.15`). Searches of the current source and available Infrastructure/Core history found no Azure upload segmentation implementation. Local Whisper's `SegmentData` values are processor transcript outputs from a single input stream, not reusable Azure audio-file splitting. Original initialization requirements discuss silence-boundary segmentation conditionally, but those requirements are not evidence that it was implemented.

### Original implementation recommendations

- Use bounded, ordered audio segments for the newly stated multi-hour requirement. For native PCM recordings, copy sample-aligned ranges into WAV segments without lossy encoding; imported formats need a deliberate decode/normalization policy, not an unsupported promise of lossless conversion.
- Bound both bytes and duration according to the selected model's input/output limits. A conservative initial target around five minutes needs validation with dense Norwegian/English speech; do not simply fill each segment to the upload ceiling. Ten minutes of native PCM is only about 19.2 MB, but dense speech can still exceed an output-token budget.
- Prefer suitable silence boundaries, provide bounded preceding-transcript context where supported, and explicitly handle continuous speech. If overlap is needed, reconcile it conservatively without deleting legitimate repeated speech.
- Stream preparation with bounded memory and temporary storage, preserve the source, and combine completed transcripts strictly in audio order. Do not silently truncate audio or keep lowering bitrate until a whole recording fits.
- Provide per-segment progress, cancellation, timeout/rate-limit handling, and resumable checkpoints. Persisted partial transcripts require an explicit privacy/retention policy; diagnostics alone must remain transcript-free.
- Validate real multi-hour input, beginning/middle/end coverage, boundary-word preservation, continuous speech, and failure/restart recovery through the real application and provider. At assessment time these journeys had not been run. The current decision above records the subsequently completed two-hour and recovery journeys; exhaustive recognition-quality checks remain unverified. Keep short dictation working without unnecessary segmentation.

An alternative is a separate Azure Speech batch-transcription provider for long files, as recommended in the Microsoft quickstart. That changes the service/authentication/job workflow and is not already supported by this Azure OpenAI adapter; do not switch providers silently.

Sources consulted on 2026-11-05: [Microsoft transcription quickstart](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/whisper-quickstart), [Microsoft audio model limits](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/concepts/models#audio-models), [OpenAI longer-input guidance](https://platform.openai.com/docs/guides/speech-to-text#longer-inputs), and [OpenAI GPT-4o Mini Transcribe model limits](https://platform.openai.com/docs/models/gpt-4o-mini-transcribe). The OpenAI model page lists a 16,000-token context window and 2,000 maximum output tokens; verify the actual Azure deployment/version contract before finalizing segment sizing. Upstream OpenAI lifecycle notices do not by themselves establish an Azure deployment retirement date.
