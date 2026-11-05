# Principles

## White-label boundary

The bundled .NET 10 console engine uses FlaUI.UIA3 5.0.0 and System.Drawing.Common 10.0.0. It has no TalkToMe.Core, Infrastructure, audio or provider dependency. Copy the entire skill folder to another repository to reuse the core; change JSON selectors and executable configuration, not the engine's brand. First NuGet restore requires access to packages; subsequent runs work offline with cached dependencies.

## Process ownership and safety

- Discover lists top-level desktop metadata; it does not click or close windows. Actions require an explicit PID or executable. Reject ambiguous windows and controls instead of selecting the first match.
- Attach mode leaves the existing process alive, even on failure. Actions can change application state; author explicit close/restore steps for dialogs and drafts. Do not assume failure automatically reverses edits.
- Launch mode never falls back to a similarly named existing process. Single-instance forwarding can cause a launch to expose no owned window; report blocked rather than attaching silently.
- The default launch policy terminates only its created process tree on success/failure. It cannot safely control child windows owned by a different PID; cross-process dialogs require a deliberate adapter.
- Never stop a user's running app merely to obtain screenshots. TalkToMe's domain driver needs an idle single-instance mutex; its adapter blocks unless stopping that app is explicitly authorized. Preserve recordings and unsaved transcript/document contents first.
- Do not automate elevation, credentials or OS permission changes. UIPI and inaccessible elevated windows are blockers, not reasons to retry with administrator access.

## UIA and timing

Use automation IDs, optional properties and exact names discovered from the real tree. Title bars may lack AutomationId. JSON actions use actual UIA Invoke/Value/Selection/Toggle patterns; unsupported patterns fail rather than falling back to blind screen coordinates or physical keyboard injection. Modal Invoke calls can block until the dialog closes: declare `opensWindowAutomationId` and a later explicit close step.

Condition waits are bounded. Native COM calls can stall beyond a polling timeout, so discovery/inspection have a 60-second watchdog and scenarios have their declared overall timeout plus 15 seconds for setup/evidence. Watchdog expiration exits the engine with 124 and records failure, with owned cleanup where configured. Application launch/attachment happens before the scenario watchdog; OS launch hangs remain an unverified boundary. Do not use arbitrary long sleeps or poll background tool executions.

## Evidence and privacy

Default trees omit names and values. `-IncludeText` records non-password UIA names, which can still contain transcripts, file paths or document content. Password-field names are omitted when IsPassword is true or unavailable. Value assertions compare internally without writing compared text into results. Screenshots require `-Screenshots`; they can expose anything visible, including secrets regardless of UIA password metadata. Never put secrets into JSON values, executable arguments, screenshots or model-readable prompts.

Tree traversal is limited to 2,000 nodes and depth 20; check its `truncated` flag and narrow the target when necessary. Captures use PrintWindow with PW_RENDERFULLCONTENT (flag 2), not a screen-region crop. Inspect the resulting image: some minimized, accelerated, protected or non-UIA windows may render blank despite success. Such technologies are not claimed validated by WPF evidence.

Paid-service declarations are authored metadata, not automatic network detection. Set `requiresPaidService: true` for any JSON journey that can incur charges. `-TaskAllowsPaidRequests` may be supplied only under current explicit or already-applicable task authorization. A new skill-authoring task alone does not authorize Azure audio uploads. Real dependencies must not be replaced with test doubles to bypass cost or availability constraints.

## Extension priority

Prioritize native file pickers and cross-process dialogs next, with an explicit owner/PID allowlist and real dialog journeys. Never relax process isolation globally. Then add real WinForms/Electron/Qt coverage and reusable visual assertions as those applications require them. These are future extensions, not capabilities claimed by this version. Make routine implementation choices autonomously; do not ask preference questions when the existing safety rules and repository evidence resolve the decision.