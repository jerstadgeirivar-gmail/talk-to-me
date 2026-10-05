# Installation principles

## Mandatory process shutdown

`TalkToMe.App` runs in the system tray and can remain active when its window is closed. Before launching any installer:

1. Find all processes named `TalkToMe.App`.
2. Terminate every matching process tree.
3. Wait for termination.
4. Query again and fail if any instance remains.

Never ask the user to close the app when the agent can terminate it. Never rely only on Inno Setup's close-applications behavior.

## Build and package

- Build from the repository root.
- Publish `src/TalkToMe.App/TalkToMe.App.csproj` as self-contained Release for `win-x64` into `artifacts/publish/win-x64`.
- Compile `packaging/TalkToMe.iss` using Inno Setup 6 and pass `/DMyAppVersion=<version>`.
- Locate `ISCC.exe` in `%LOCALAPPDATA%\Programs\Inno Setup 6` first, then the Program Files locations. Inno Setup may be installed per-user.
- The expected installer is `artifacts/installer/TalkToMe-Setup.exe`.
- Generate and inspect a SHA-256 checksum.

## Install destination and safety

- The per-user destination is `%LOCALAPPDATA%\Programs\TalkToMe`.
- Installation requires no elevation.
- Use `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /CLOSEAPPLICATIONS` for unattended installation.
- Silent installation must not auto-launch TalkToMe. The `[Run]` entry uses `skipifsilent`; launch the installed executable explicitly for validation.
- Use a log file under `artifacts/validation` and inspect it if installation fails.
- The installer has a stable per-user AppId. Do not redirect a production-user install into an evidence directory because that rewrites uninstall and startup registration.

## Validation

- Verify `%LOCALAPPDATA%\Programs\TalkToMe\TalkToMe.App.exe` exists.
- Verify its product version matches the package version.
- Launch that exact installed executable, not a build or publish output.
- Before each live UI journey, confirm no other `TalkToMe.App` instance holds the single-instance mutex; after stopping an instance, wait and query again before launching the driver.
- Build the solution, then exercise normal and relevant failure/recovery journeys against the real Debug app before packaging and the exact installed binary afterward. Use real component boundaries and dependencies, not unit tests, mocks, stubs, simulated executables, or a diagnostic transcript as evidence of real transcription.
- On every install, run real-audio transcription with the installed app and a real provider; verify a nonempty transcript. When using import, also verify the original file is preserved. Repeat against Debug when transcription or audio handling changed. A picker closing or a record-only result does not establish successful transcription. If a microphone, provider, or service is unavailable, report that journey blocked instead of substituting a test double.
- Store screenshots, UIA trees, result files, and failure details below `artifacts/validation`. Inspect the visible status and artifacts, not only a process exit or driver PASS line. Stop a stalled driver, retain its evidence, and report the failed or unverified journey without claiming a pass.
- After driver runs, launch the installed app normally and verify a responsive window and a live process independently of the installer and test harness. Report any later exit or untested update journey separately.