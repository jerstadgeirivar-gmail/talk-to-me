# Threat Model

| Risk | Mitigation | Residual risk |
| --- | --- | --- |
| Provider-secret disclosure | Namespaced current-user DPAPI; masked replacement; legacy Azure migration; no key/token in JSON, logs, screenshots, or package | Malware running as the same user can access user-scoped secrets |
| Malicious endpoint | Azure requires HTTPS; local-server HTTP is allowed only for loopback; non-loopback servers require HTTPS | A user-configured HTTPS endpoint can still be untrusted |
| Local model download/tampering | Explicit consent; HTTPS pinned source; pinned size and SHA-256 before atomic install and every load | A same-user process can replace both app state and model between checks; first setup needs network access |
| Audio sent to text-only endpoint | LM Studio/Ollama adapters never post audio; capability UI states the limitation | A future audio API requires a new reviewed adapter |
| Transcript/audio leakage | No telemetry; no transcript/audio diagnostics; per-user data directory; successful and failed audio deletion by default | Failed audio can be retained by explicit opt-in; interrupted sessions can leave recoverable audio |
| Wrong-window insertion | Capture HWND and PID at start; validate and reactivate exact target | Target applications can change internal document/tab state |
| Clipboard races | Bounded retries; restore after success, activation failure, paste failure, or cancellation while temporary text still owns clipboard | Custom clipboard formats may not restore perfectly across all applications |
| Elevated target | Do not elevate TalkToMe; rely on Windows UIPI | Paste into elevated applications is unsupported |
| Dependency compromise | Pinned Whisper.net/runtime/model, model checksum, licenses, Windows CI restore/build/test | NuGet and upstream model supply-chain risk remains |
| Settings corruption | Atomic temporary write and replace | Manual external edits can still produce invalid values |
| Release secret inclusion | Secret source is deleted and ignored; package inspection required | Pattern scans cannot prove absence of every possible secret |

No analytics, remote backend, transcript history, or always-listening capture is implemented.
