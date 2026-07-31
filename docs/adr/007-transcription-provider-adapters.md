# ADR 007: Deterministic Transcription Provider Adapters

## Status

Accepted 2026-07-31.

## Decision

Keep `ITranscriptionProvider` as the application port. Centralize stable IDs, descriptors, validation, construction, capability tests, and failure normalization in `ITranscriptionProviderFactory`/`TranscriptionProviderRegistry`. A coordinator owns the selected adapter, serializes replacement against active transcription, and caches Local Whisper so repeated dictation avoids model reload.

Selection is deterministic: an explicit ID wins; a legacy configuration with complete Azure settings and protected key migrates once to `azure-openai`; otherwise a new configuration is saved as `local-whisper`. Unknown, incomplete, missing, corrupt, unreachable, or incapable selections fail in place. No adapter fallback exists.

## Providers

- `local-whisper`: Whisper.net 1.9.1 CPU runtime over whisper.cpp with multilingual `small-q5_1`; language `no`, vocabulary prompt, cancellation, model checksum verification, and normalized line endings. To keep the installer small, the model is acquired through an explicit first-run consent dialog with progress/cancellation and atomic checksum-verified installation.
- `azure-openai`: preserved REST routes, API version/environment overrides, request IDs, timeouts, size limit, and typed HTTP failures.
- `lm-studio`: separate profile and reachability test. The official LM Studio 0.4 API documentation lists chat, responses, completions, embeddings, model listing/loading/download/unload, and Anthropic messages, but no audio transcription endpoint.
- `ollama`: separate profile and reachability test. The official Ollama API lists generate, chat, embed, model management, tags/process/version, and compatibility endpoints, but no audio transcription endpoint.

LM Studio and Ollama therefore return `CapabilityUnavailable` and never post audio to a chat/text endpoint. Their UI fields reserve the adapter boundary for a future documented contract without claiming present support.

## Security

Credentials use provider-namespaced DPAPI files. The legacy Azure credential migrates on access without plaintext exposure. HTTP is permitted only for loopback local-server URLs; non-loopback endpoints require HTTPS. Local Whisper does not make a network call during transcription.

## Sources verified

- https://lmstudio.ai/docs/developer/rest
- https://docs.ollama.com/api/introduction
- https://www.nuget.org/packages/Whisper.net/1.9.1
- https://huggingface.co/ggerganov/whisper.cpp
