# TalkToMe adapter

Use the generic core for PID inspection and JSON-only UI mechanics. Use [invoke-talk-to-me.ps1](../scripts/invoke-talk-to-me.ps1) for existing production journeys; it delegates to `tools/TalkToMe.UiDriver`, preserving its domain assertions, source hashing, encrypted resume checks, file-picker semantics and privacy validation.

## Scenario routing

| Scenario | Existing driver mode | Prerequisites |
| --- | --- | --- |
| `RecordOnly` | Real file-backed recording journey | Real source audio; no paid transcription or diagnostic transcript. |
| `Provider` | `--live-provider` | Actual saved provider/settings/key; submits short readiness audio. |
| `Profiles` | `--segmentation` | Actual long-enough audio and configured Azure key; cancellation/restart and profile assertions. |
| `TokenLimit` | `--segmentation-token-limit` | Dense real speech that exercises returned usage and subdivision. |
| `AzureRepair` | `--azure-error-retry` | Real Azure service; nonexistent deployment in isolated settings, repair and Retry. |
| `Vocabulary` | `--vocabulary-retry` | Real configured service and actual UTF-8 file failure/repair. |
| `Long` | `--segmentation-long` | Exactly two hours of real audio; rejects longer input before submitting requests. |

The default executable is the installed app. Supply `-ExecutablePath` for a built Debug binary and `-AudioPath` except for Provider. Evidence directories must be fresh. All modes except RecordOnly require `-TaskAllowsPaidRequests`, which reflects already-applicable task authorization rather than requiring a new permission prompt on every command.

If TalkToMe is running, prefer PID inspection. Domain journeys block by default rather than closing it. `-StopRunningApplication` requires explicit authorization and an assessment of active recording/unsaved content; it stops and verifies all TalkToMe instances before the driver. It does not install anything. Every install/upgrade must use the [install-talk-to-me skill](../../install-talk-to-me/SKILL.md), including mandatory shutdown and installed-binary verification.

## Common operations

- Inspect: `invoke-uia.ps1 -Action Inspect -ProcessId <pid> -WindowAutomationId MainWindow -EvidenceDirectory <new-directory> -Screenshots`.
- Settings-only: [talk-to-me-settings.json](../assets/talk-to-me-settings.json) with Run and an explicit PID opens Settings, inspects/selects without saving, closes the modal and leaves the app alive. It does not test Azure or persist provider changes.
- Recording: `invoke-talk-to-me.ps1 -Scenario RecordOnly -AudioPath <real-audio> -EvidenceDirectory <new-directory>` when the mutex is free.
- Paid recovery: add `-TaskAllowsPaidRequests` only when already authorized, choosing the smallest relevant real fixture. Do not rerun the two-hour case against both Debug and installed builds merely for reassurance.
- Two-hour preparation: the existing driver supports `--extract-audio <source> <new-output.wav> 7200`; it uses real NAudio decode offline and does not call Azure.

## Windows picker and settings gotchas

Windows file pickers use filename Edit ID `1148` and Open ID `1`, which can be Button or SplitButton. Do not click the first Edit or first Open label. A settings modal invoked through UIA may block until closed; the generic sample declares that explicitly. Settings is intentionally scrollable: validate reachable provider/profile/test/save controls, not an obsolete no-scrolling requirement. Closed ComboBoxes can omit their selected item from UIA; expand before checking selection and ensure item names are accessible.

Keep provider/service-specific checks in the existing driver, not the white-label engine. Never replace unavailable Azure credentials with fake endpoints, synthetic transcripts or mocked HTTP responses. Short readiness success does not prove long-file coverage or correct model limits. Read `result.txt`, `segmentation-result.txt`, `retry-result.txt`, runtime request IDs and screenshots before reporting domain success.