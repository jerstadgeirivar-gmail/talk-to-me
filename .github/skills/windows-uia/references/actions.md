# JSON journeys

## Entry point

`invoke-uia.ps1 -Action Run -ScenarioPath <json> -EvidenceDirectory <fresh-directory>` launches the declared executable. Adding `-ProcessId <pid>` overrides launch and attaches safely. Use Windows PowerShell 5.1 or newer and .NET 10. The wrapper builds the bundled project, so agents do not generate ad-hoc scripts.

`application.executable` may be absolute, relative to the JSON file, or use `%ENVIRONMENT_VARIABLE%`. `application.arguments` is an array; no shell is invoked. `application.windowAutomationId` identifies the root window; omit it only if exactly one owned window exists. `application.stopOwnedProcess` defaults to true for launch and is never applied to attached processes.

Scenarios have 1-100 steps, a 1-600 second overall budget and per-step budgets of 1-120 seconds (default 10). Unknown JSON fields fail validation. Use [template](../assets/journey-template.json) for another app.

## Actions

| Action | Inputs and behavior |
| --- | --- |
| `invoke` | Exact `automationId` and/or `name`; actual Invoke pattern. For modal actions, add `opensWindowAutomationId` and explicitly close that dialog later. |
| `setValue` | `value`; Value pattern only. Password fields and controls without safe password metadata are rejected. Do not enter secrets. |
| `select` | With `value`, selects that actual ComboBox item name. Without `value`, selects the target tab/list/radio item through SelectionItem. |
| `toggle` | `checked` boolean sets a CheckBox's desired state. |
| `wait`, `assert` | Presence by default; optionally `enabled`, `nameEquals`, `nameContains`, `valueEquals`, `checked` or `exists: false`. Conditions retry until the bounded timeout. |
| `capture` | Writes a bounded tree and, only with the screenshots flag, a PNG. No control selector required. |

Each step can set `windowAutomationId` for an owned top-level or descendant dialog. Selectors must be unique within that window and belong to the target PID. There are no arbitrary scripts, regex selectors, file-picker coordinate fallbacks, shell actions or transcript substitutes. A domain adapter is the home for service assertions, media handling and cross-process workflows.

## Evidence

`process.json` identifies ownership. `before-tree.json`, `after-tree.json` and optional PNGs capture the journey. `steps.json` records completed step indexes/actions without input values. `result.json` reports PASS/FAIL; failures identify completed-step count and exception type. `failure-tree.json` and optional image are attempted on failure. `cleanup.json` records owned termination, and `watchdog.json` identifies a stalled engine. An absent result or watchdog record is not PASS.

## First real smoke journey

Set `WINDOWS_UIA_TARGET_EXE` to an existing real editor executable compatible with the [editor sample](../assets/editor-journey.json). In this repository it is `tools/TalkToMe.TestTarget/bin/Debug/net10.0-windows/TalkToMe.TestTarget.exe`. The sample writes and reads a real WPF text control and captures the window, then terminates only that created editor. It is a real UI component, not a replacement transcription service or simulated executable. The [timeout sample](../assets/editor-timeout.json) intentionally waits for an absent control, retaining FAIL evidence and verifying cleanup.