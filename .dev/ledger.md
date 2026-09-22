# Active Development Ledger

## 2026-09-22

- Added the active `.dev` implementation plan and ledger for Azure audio transport handling.
- Implemented source-preserving Azure upload preparation for oversized WAV recordings.
- Added temporary MP3 upload artifacts using NAudio and Windows Media Foundation.
- Kept recordings at or below the preparation threshold on the original WAV upload path.
- Added staged transcription status reporting for preparation, sending, and response waiting.
- Added a diagnostic no-delay mode so full-duration integration fixtures can be processed quickly without changing production playback behavior.
- Added the Whisper.net CUDA 12 runtime and configured Local Whisper to prefer GPU inference with CPU fallback.
- Added runtime backend reporting so the application can distinguish CUDA from CPU fallback.
- Verified the affected application and UI driver builds.
- Verified a real Local Whisper application journey after the GPU runtime integration.
