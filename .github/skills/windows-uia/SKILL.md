---
name: windows-uia
description: "Use for: reusable Windows UI Automation inspection, PID-scoped desktop testing, FlaUI UIA3 journeys, PrintWindow screenshots, modal dialogs, control discovery, evidence capture, and TalkToMe real-app validation. WHEN: Windows UIA, inspect Windows app, test desktop UI, capture app window, automate WPF, run UI journey, debug Windows controls, validate TalkToMe, reuse GUI tests, extend Windows UIA skill. White-label core plus app-specific adapters; avoid recreating inline scripts."
argument-hint: "Inspect PID 1234, or run a JSON journey with an evidence directory; name the app and authorized side effects."
---

# Windows UIA

Use this repository-owned bundle for Windows GUI work. The core is app-neutral; TalkToMe is its first adapter. No unit tests, test doubles, simulated executables or invented service responses are permitted.

## Route and load

1. Identify an explicit target PID or executable, the desired journey, evidence sensitivity and authorized side effects. Do not infer consent to close apps, overwrite documents or submit paid requests from a request to inspect a window.
2. Load [principles](./references/principles.md). Use [JSON actions](./references/actions.md) only when creating or running a generic journey. Use the [TalkToMe adapter](./references/talk-to-me.md) only for its domain scenarios. Do not load all resources upfront.
3. Use a fresh ignored evidence directory. Load [checklist](./references/checklist.md) for the completion gate. Install/upgrade requests must also load the existing application installation skill through the adapter reference.

## Run

1. For discovery, call [invoke-uia.ps1](./scripts/invoke-uia.ps1) with `-Action Discover -EvidenceDirectory <new-directory>`. Add `-IncludeText` only when window names may be recorded.
2. For inspection, use `-Action Inspect -ProcessId <pid> -WindowAutomationId <id> -EvidenceDirectory <new-directory>`. Add `-Screenshots` only when visible content may be captured. A hidden tray app must be shown through its normal activation workflow first.
3. For actions, adapt [journey-template.json](./assets/journey-template.json), then use `-Action Run -ScenarioPath <json> -EvidenceDirectory <new-directory>`. `-ProcessId` attaches without closing the app; otherwise the JSON executable is launched and ownership applies. [editor journey](./assets/editor-journey.json), [timeout journey](./assets/editor-timeout.json) and [TalkToMe Settings journey](./assets/talk-to-me-settings.json) are focused examples described in the references.
4. For TalkToMe domain journeys, use [invoke-talk-to-me.ps1](./scripts/invoke-talk-to-me.ps1). Existing task authorization can permit `-TaskAllowsPaidRequests`; the flag records that authorization, it does not grant it. Never run a paid journey solely to validate this skill. Long paid validation stays capped at two hours before submission.
5. Inspect JSON results, UIA trees and permitted screenshots using the image tool. A successful command, nonempty tree or PrintWindow return is not proof that the journey or image is correct.

## Extend and validate

1. Reuse JSON selectors/actions first. Extend the core only for app-neutral operations; put app-specific state, services and assertions in an adapter or its existing driver.
2. Run [validate-skill.ps1](./scripts/validate-skill.ps1) for metadata, resource links, PowerShell syntax and JSON assets. Use [verify-live.ps1](./scripts/verify-live.ps1) for real editor success/timeout/privacy/ownership checks and optional attached/adapter safeguards. Run changed behavior against real applications. [Core project](./scripts/WindowsUia/WindowsUia.csproj), [entry point](./scripts/WindowsUia/Program.cs) and [scenario implementation](./scripts/WindowsUia/ScenarioRunner.cs) are loaded only for code changes.
3. Record verified commands, evidence and gaps in the repository. Do not automatically commit, push or install. Do not report blocked or interrupted tests as passed.

## Completion gate

The selected journey and relevant failure/recovery checks must pass against real applications; source and attached-process ownership must be preserved; evidence must be inspected. Report the executable/PID, scenario, result, evidence directory and untested boundaries. This skill supports UIA-exposed applications, not every Windows app or unsupported control pattern.