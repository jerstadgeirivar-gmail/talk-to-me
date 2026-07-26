# Requirements Status

| Area | Status | Evidence or remaining work |
| --- | --- | --- |
| WPF/UI Automation shell | Verified | Slice A evidence; stable IDs, bounds, screenshots, PID-scoped lifecycle |
| File-backed audio pipeline | Verified | Slice B; paced NAudio decode, shared frames, valid WAV |
| Real WASAPI source | Implemented, pending device test | Same frame boundary; physical microphone and device invalidation not exercised |
| Azure REST provider | Contract-verified, live blocked | Four tests pass; endpoint/deployment absent and Azure CLI requires interactive sign-in |
| Norwegian transcript in UI | Diagnostic path verified | Live semantic anchors pending Azure configuration |
| Global hotkey | Verified in functional journey | `Ctrl+Alt+F9`, `MOD_NOREPEAT`, deterministic unregister |
| Original-target insertion | Verified with isolated WPF target | Exact Unicode match, no Enter, clipboard ownership logic |
| Notepad/VS Code insertion | Pending environment smoke | Packaged Notepad reused unrelated user process; VS Code not exercised |
| Protected credential/settings | Verified | DPAPI, atomic JSON, masked UI, plaintext source removed |
| Tray and single instance | Implemented, limited verification | Tray actions and activation signal compile; notification-area interaction not automated |
| Recovery | Partially verified | Retain/retry/delete/cancel implemented; crash metadata, startup discovery, retention limit incomplete |
| Overlay | Not complete | Main status window is accessible but is not a non-activating multi-monitor overlay |
| Settings breadth | Partial | Endpoint/deployment/key/glossary/retention/startup flags and validated global hotkey editor; microphone editor and connection test incomplete |
| Startup registration | Not complete | Setting is persisted but registry/startup task is not applied |
| Audio size hardening | Not complete | Provider rejects over 25 MB; warning/compression/segmentation absent |
| Packaging | Verified | Self-contained `win-x64`, package secret scan, two fresh functional runs |
