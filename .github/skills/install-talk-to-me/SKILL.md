---
name: install-talk-to-me
description: "Install, reinstall, upgrade, or locally deploy the TalkToMe Windows application. Use for: build installer, install locally, reinstall TalkToMe, upgrade installed app, deploy and install, silent installer. WHEN: install it, install locally, deploy locally, run setup, update installed TalkToMe. Always kills and verifies all TalkToMe.App processes before starting the installer, then verifies the installed executable and live application."
argument-hint: "Build and install the current branch locally, then verify the installed app"
---

# Install TalkToMe

Use this workflow for every local installation. The process-stop precondition is mandatory and must not be delegated to the user.

## Procedure

1. Read [installation principles](./references/principles.md).
2. Ensure requested source changes are complete and the working tree state is understood.
3. Select a three-part version greater than the currently installed or latest released version.
4. Build the solution and run the relevant real UI journeys before packaging.
5. Publish a self-contained `win-x64` Release build and compile `packaging/TalkToMe.iss` with Inno Setup.
6. Run [install-local.ps1](./scripts/install-local.ps1) with the generated installer path. The script must stop every `TalkToMe.App` process, verify termination, install silently, and verify the installed executable.
7. Launch the installed executable and run the affected live journeys against that exact binary. Inspect generated evidence rather than inferring success from exit codes.
8. Record reusable commands, results, and evidence locations in tracked repository documentation.
9. Complete every item in [the checklist](./references/checklist.md).

## Completion gate

Do not report installation as successful unless the old process was confirmed stopped, the installer exited successfully, the installed binary exists with the expected version, and the installed application completed a live validation journey.