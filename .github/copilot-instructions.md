# TalkToMe

## Purpose

TalkToMe is a .NET 10 WPF dictation utility for Windows 11. It records speech, transcribes with Local Whisper or Azure OpenAI, and inserts text into the original target application.

## Primary workflows

- Use `.github/skills/windows-uia/SKILL.md` for Windows UIA inspection, screenshots and real-app GUI journeys. Reuse its scripts/JSON scenarios; extend the tested app-neutral core or an app adapter instead of generating repeated inline automation code.
- Use `.github/skills/install-talk-to-me/SKILL.md` for every local install, reinstall, upgrade, or deployment to the installed application directory.
- Follow `AGENTS.md` for mandatory real-application validation.

## Tech stack and conventions

- .NET 10, WPF, C#, Whisper.net, FlaUI UIA3, and Inno Setup.
- Keep provider settings in `ApplicationSettings`; credentials remain in the DPAPI secret store.
- Proceed autonomously with routine implementation choices and educated guesses grounded in the repository. Do not ask preference questions; report unavoidable blockers with the best available alternative.
- Never launch an installer while `TalkToMe.App` is running. Stop all instances and verify termination first.
- Do not commit or push unless explicitly requested.