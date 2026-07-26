# Threat Model

| Risk | Mitigation | Residual risk |
| --- | --- | --- |
| API-key disclosure | Current-user DPAPI; masked replacement; no key in JSON, command arguments, logs, screenshots, or package | Malware running as the same user can access user-scoped secrets |
| Malicious endpoint | Absolute HTTPS validation; no HTTP endpoints | A valid HTTPS endpoint can still be untrusted if the user configures it |
| Transcript/audio leakage | No telemetry; no transcript/audio diagnostics; per-user data directory; successful audio deletion | Failed audio is retained until recovery cleanup |
| Wrong-window insertion | Capture HWND and PID at start; validate and reactivate exact target | Target applications can change internal document/tab state |
| Clipboard races | Bounded retries; restore only while temporary text still owns clipboard | Custom clipboard formats may not restore perfectly across all applications |
| Elevated target | Do not elevate VoiceType; rely on Windows UIPI | Paste into elevated applications is unsupported |
| Dependency compromise | Small dependency set; pinned package versions; Windows CI restore/build/test | NuGet supply-chain risk remains |
| Settings corruption | Atomic temporary write and replace | Manual external edits can still produce invalid values |
| Release secret inclusion | Secret source is deleted and ignored; package inspection required | Pattern scans cannot prove absence of every possible secret |

No analytics, remote backend, transcript history, or always-listening capture is implemented.
