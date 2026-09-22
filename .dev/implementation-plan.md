# Active Implementation Plan: Azure Audio Transport

## Goal

Keep the existing mono 16 kHz, 16-bit PCM WAV as the canonical local recording. Only transform it for an Azure request when it is above the conservative 24 MB preparation threshold.

## Current release

- Small files stay byte-for-byte unchanged and upload as WAV.
- Large files are encoded to a temporary mono MP3 using NAudio and Windows Media Foundation.
- The initial target is 32 kbit/s. Increase to 40 or 48 kbit/s only if representative accuracy checks show measurable degradation.
- The source WAV is never overwritten or deleted by transport preparation.
- Temporary upload artifacts are deleted after success, failure, cancellation, and disposal.
- Chunking is a rare fallback only when the compressed artifact still exceeds the safe Azure request limit. Routine ten-minute splitting is explicitly out of scope.
- The GUI uses simple staged status text while preparation and transcription are active.

## Work items

- [ ] Inspect the supplied fixture and record metadata/hash.
- [ ] Verify Windows Media Foundation exposes a suitable mono 32 kbit/s MP3 encoder.
- [ ] Implement source-preserving Azure upload preparation.
- [ ] Wire MIME type, filename, size validation, and cleanup into the Azure provider.
- [ ] Add simple preparation/upload/waiting status reporting.
- [ ] Add rare oversized-compressed chunk fallback only if needed by the real fixture.
- [ ] Build and run real integration validation.
- [ ] Upload the supplied oversized fixture to Azure when credentials are available.
- [ ] Reconcile stable documentation after implementation is verified.

## Runtime gates

- The actual Windows Media Foundation encoder selection and bitrate must be measured, not assumed.
- Azure credentials and deployment configuration must be available for the live request.
- Accuracy must be compared on representative recordings before changing the initial bitrate.
