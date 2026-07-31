# Requirements Status

| Area | Status | Evidence or remaining work |
| --- | --- | --- |
| WPF/UI Automation shell | Verified | Slice A evidence; stable IDs, bounds, screenshots, PID-scoped lifecycle |
| File-backed audio pipeline | Verified | Slice B; paced NAudio decode, shared frames, valid WAV |
| Real WASAPI source | Implemented, pending device test | Same frame boundary; physical microphone and device invalidation not exercised |
| Local Whisper provider | Live verified | Whisper.net 1.9.1 CPU, pinned multilingual small-q5_1, real Norwegian fixture and transcript screenshot |
| Azure REST provider | Contract-verified, live blocked | Existing tests pass; no endpoint/deployment/credential is available locally |
| LM Studio provider | Honest capability verified | Installed app found; server could not be made reachable; official 0.4 API has no STT and adapter sent no audio |
| Ollama provider | Honest capability verified | Live Ollama 0.31.1 reachable; official API has no STT and adapter sent no audio |
| Norwegian transcript in UI | Live Local verified | 24-second production WAV produced non-empty semantically stable Norwegian text |
| Global hotkey | Verified in functional journey | `Ctrl+Alt+F9`, `MOD_NOREPEAT`, deterministic unregister |
| Original-target insertion | Verified with isolated WPF target | Exact Unicode match, no Enter, clipboard ownership logic |
| Notepad/VS Code insertion | Pending environment smoke | Packaged Notepad reused unrelated user process; VS Code not exercised |
| Protected credential/settings | Verified | Namespaced DPAPI, legacy Azure migration, atomic JSON, masked UI, plaintext source removed |
| Tray and single instance | Implemented, limited verification | Tray actions and activation signal compile; notification-area interaction not automated |
| Recovery | Partially verified | Retain/retry/delete/cancel implemented; crash metadata, startup discovery, retention limit incomplete |
| Overlay | Not complete | Main status window is accessible but is not a non-activating multi-monitor overlay |
| Settings breadth | Provider feature verified | Tabbed conditional provider UI, readiness/capability test, repair, accessibility IDs, protected fields; microphone editor remains incomplete |
| Startup registration | Not complete | Setting is persisted but registry/startup task is not applied |
| Audio size hardening | Not complete | Provider rejects over 25 MB; warning/compression/segmentation absent |
| Packaging | Verified | Model absent from publish/install; 55,960,288-byte installer; installed first-run consent/progress/verified setup and live transcription pass |
