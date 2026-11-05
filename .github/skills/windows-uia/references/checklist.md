# Completion checklist

- Discovery metadata names this folder, includes trigger phrases and fits the 1,024-character limit.
- SKILL.md orchestrates with relative links; detailed platform/domain knowledge stays in references.
- `validate-skill.ps1` passes; all scripts parse and JSON assets load.
- `verify-live.ps1` passes for the actual editor executable and selected attached-app safeguards, without paid requests.
- Generic project builds without TalkToMe dependencies; first package restore prerequisites are disclosed.
- Real discovery/inspection identifies the intended PID and window; hidden/ambiguous windows are reported, not guessed.
- Real JSON success verifies observable UI state and inspects permitted captures.
- Timeout/absent-control failure produces retained FAIL evidence and cleans up only its created app.
- Attached app remains alive; modals opened by the journey are closed explicitly; persistent settings/documents are not changed accidentally.
- Password fields, default text evidence and screenshots follow the sensitivity policy.
- Paid journeys use only already-authorized services and real audio; two-hour cost ceiling remains enforced. Skill creation does not authorize paid tests.
- TalkToMe adapter blocking and command routing are checked; service-dependent journeys not run are disclosed rather than called passed.
- Root AGENTS.md and .github/copilot-instructions.md route future Windows GUI work to this skill while retaining installer/testing requirements.
- Evidence and reusable results are recorded in tracked repository documentation; private trees, images, audio and transcripts remain ignored.
- Disclose untested frameworks, cross-process dialogs, watchdog/OS-launch edge cases and service journeys. No automatic commit/push/install.