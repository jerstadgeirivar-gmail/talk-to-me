# Work Routing

How to decide who handles what.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Architecture and cross-cutting changes | Lead | Core/Infrastructure/App boundaries, service contracts, state flow, sequencing, review |
| WPF UI and interaction | UI | XAML, views, view models, settings/startup UX, user-facing validation and recovery |
| Windows and transcription | Platform | Audio, hotkeys, focus/clipboard insertion, Local Whisper, Azure OpenAI, secure persistence |
| Tests and live validation | Tester | Unit/integration tests, `TalkToMe.TestTarget`, `TalkToMe.UiDriver`, evidence from real app journeys |
| Packaging and operations | Release | Inno Setup, publish/build workflows, install/upgrade, installed executable verification |
| Work monitoring and handoffs | Ralph | Stale assignments, missing owners or acceptance criteria, blocker follow-up, validation gaps |
| Security and responsible AI | Rai | Secrets, privacy, harmful or deceptive behavior |
| Verification and assumptions | Fact Checker | Claims, package/API existence, pre-mortems |

Preset installation adds concrete routes for the configured team. Add or edit rows
here only when their agent names also exist in the casting registry.

## Ownership and Handoffs

- Lead names the primary owner for cross-cutting work and keeps contracts aligned across `src/TalkToMe.Core`, `src/TalkToMe.Infrastructure`, and `src/TalkToMe.App`.
- UI and Platform coordinate at the view-model/service boundary; UI owns presentation, while Platform owns Windows and provider behavior.
- Tester validates behavior-changing work against the built application, including a relevant recovery path, and records evidence.
- Release owns installer operations and must prove that no `TalkToMe.App` process is running before installation.
- Ordinary settings stay in `ApplicationSettings`; credentials stay in the DPAPI-backed secret store. Rai reviews security-sensitive changes.

## Issue Routing

| Label | Action | Who |
|-------|--------|-----|
| `squad` | Triage: analyze issue, assign `squad:{member}` label | Lead |
| `squad:{name}` | Pick up issue and complete the work | Named member |

### How Issue Assignment Works

1. When a GitHub issue gets the `squad` label, the **Lead** triages it — analyzing content, assigning the right `squad:{member}` label, and commenting with triage notes.
2. When a `squad:{member}` label is applied, that member picks up the issue in their next session.
3. Members can reassign by removing their label and adding another member's label.
4. The `squad` label is the "inbox" — untriaged issues waiting for Lead review.

## Rules

1. **Eager by default** — spawn all agents who could usefully start work, including anticipatory downstream work.
2. **Scribe always runs** after substantial work, always as `mode: "background"`. Never blocks.
3. **Quick facts → coordinator answers directly.** Don't spawn an agent for "what port does the server run on?"
4. **When two agents could handle it**, pick the one whose domain is the primary concern.
5. **"Team, ..." → fan-out.** Spawn all relevant agents in parallel as `mode: "background"`.
6. **Anticipate downstream work.** If a feature is being built, spawn the tester to write test cases from requirements simultaneously.
7. **Issue-labeled work** — when a `squad:{member}` label is applied to an issue, route to that member. The Lead handles all `squad` (base label) triage.
