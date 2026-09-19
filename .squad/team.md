# Squad Team

> TalkToMe is a .NET 10 WPF dictation utility for Windows 11 that records speech, transcribes it through Local Whisper or Azure OpenAI, and inserts the result into the application that originally had focus.

## Coordinator

| Name | Role | Notes |
|------|------|-------|
| Squad | Coordinator | Routes work, enforces handoffs and reviewer gates. |

## Members

| Name | Role | Charter | Status |
|------|------|---------|--------|
| Lead | Architecture, Boundaries, and Coordination | `.squad/agents/lead/charter.md` | ✅ Active |
| UI | WPF UI, ViewModels, and User Journeys | `.squad/agents/ui/charter.md` | ✅ Active |
| Platform | Windows Integration, Providers, and Secure Persistence | `.squad/agents/platform/charter.md` | ✅ Active |
| Tester | Automated and Real-Application Validation | `.squad/agents/tester/charter.md` | ✅ Active |
| Release | Packaging, Installation, and Operations | `.squad/agents/release/charter.md` | ✅ Active |
| Scribe | Session Logger and Decision Merger | `.squad/agents/scribe/charter.md` | 📋 Silent |
| Ralph | Work Monitor and Handoff Tracker | `.squad/agents/ralph/charter.md` | 🔄 Monitor |
| Rai | RAI Reviewer | `.squad/agents/Rai/charter.md` | 🛡️ Background |
| Fact Checker | Verification and Devil's Advocate | `.squad/agents/fact-checker/charter.md` | 🔍 Verifier |


## Coding Agent

<!-- copilot-auto-assign: false -->

| Name | Role | Charter | Status |
|------|------|---------|--------|
| @copilot | Coding Agent | — | 🤖 Coding Agent |

### Capabilities

**🟢 Good fit — auto-route when enabled:**
- Bug fixes with clear reproduction steps
- Test coverage (adding missing tests, fixing flaky tests)
- Lint/format fixes and code style cleanup
- Dependency updates and version bumps
- Small isolated features with clear specs
- Boilerplate/scaffolding generation
- Documentation fixes and README updates

**🟡 Needs review — route to @copilot but flag for squad member PR review:**
- Medium features with clear specs and acceptance criteria
- Refactoring with existing test coverage
- API endpoint additions following established patterns
- Migration scripts with well-defined schemas

**🔴 Not suitable — route to squad member instead:**
- Architecture decisions and system design
- Multi-system integration requiring coordination
- Ambiguous requirements needing clarification
- Security-critical changes (auth, encryption, access control)
- Performance-critical paths requiring benchmarking
- Changes requiring cross-team discussion

## Project Context

- **Project:** talk-to-me
- **Owner:** Geir Ivar Jerstad
- **Stack:** .NET 10, C#, WPF, Whisper.net, FlaUI UIA3, Inno Setup, Windows 11
- **Description:** Desktop dictation utility with Local Whisper or Azure OpenAI transcription and focus-aware text insertion.
- **Created:** 2026-09-17T15:58:13.3409280+02:00
