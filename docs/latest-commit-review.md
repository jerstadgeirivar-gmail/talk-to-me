# Latest Commit Review

**Review date:** 2026-09-17

## Scope

Read-only review of committed range `cac6f86^..0320bda`, covering the two latest substantive commits. Uncommitted Squad scaffolding was excluded from the review.

## Executive summary

The reviewed changes contain two runtime update-lifecycle defects: automatic updates can leave TalkToMe stopped, and cancelling an update check can leave `gh.exe` running. The provider and settings changes also lack targeted boundary and persistence validation. Whitespace validation passed; full build and live validation were unavailable because the environment has only .NET SDK 9 while the solution targets .NET 10, and no built binaries were available.

## Findings

| Priority | Locations | Issue | Impact | Remediation direction |
| --- | --- | --- | --- | --- |
| P1 | `packaging/TalkToMe.iss:40-41`; `src/TalkToMe.App/GitHubUpdateService.cs:126-132`; `src/TalkToMe.App/UpdateCoordinator.cs:82-87` | Automatic updates use `/VERYSILENT`; the Inno Setup `[Run]` entry uses `skipifsilent`; and the updater shuts down. | TalkToMe is not restarted after an automatic update. | Make the automatic-update path preserve or explicitly perform the intended restart, and validate the packaged update journey. |
| P2 | `src/TalkToMe.App/GitHubUpdateService.cs:167-176`; `src/TalkToMe.App/UpdateCoordinator.cs:39-50` | Cancelling update checks does not kill and await the `gh.exe` process tree. | A cancelled check can leave `gh.exe` running. | Track the child process and ensure cancellation terminates and awaits the process tree before returning. |
| P2 | `src/TalkToMe.Infrastructure/TranscriptionProviderCoordinator.cs:57-90` | No provider-boundary tests cover Auto, Norwegian, bilingual language mapping, prompt construction, or retry behavior. | Mapping, prompt, or retry regressions can pass without detection. | Add focused provider-boundary tests for each mapping, prompt, and retry path. |
| P2 | `src/TalkToMe.Infrastructure/JsonApplicationSettingsStore.cs:61-65` | No automated coverage exercises absent, null, or unknown language settings across save/reopen. | Settings compatibility and fallback behavior can regress unnoticed. | Add save/reopen tests for absent, null, and unknown language values and assert the defined fallback. |

## Validation

- `git diff --check` passed.
- Full build and live validation could not run: the environment has only .NET SDK 9, while the solution targets .NET 10, and no built binaries were available.

## Recommended next steps

1. Fix the automatic-update restart behavior and validate a real packaged update journey, including shutdown and restart.
2. Fix cancellation cleanup so `gh.exe` and its process tree are terminated and awaited.
3. Add the targeted transcription-provider and settings persistence tests, then run the relevant live update journey when a .NET 10 build and binaries are available.
