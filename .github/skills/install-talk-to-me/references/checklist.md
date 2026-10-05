# Local installation checklist

- [ ] Source changes and repository status reviewed
- [ ] Solution build succeeds
- [ ] Real Debug application normal and relevant failure/recovery journeys pass, with no competing app instance
- [ ] Self-contained Release publish succeeds
- [ ] Inno Setup compilation succeeds
- [ ] Installer SHA-256 generated
- [ ] Every `TalkToMe.App` process terminated before installer launch
- [ ] A second process query confirms no instance remains
- [ ] Silent installer exits with code 0
- [ ] Install log retained under `artifacts/validation`
- [ ] Installed executable exists in `%LOCALAPPDATA%\Programs\TalkToMe`
- [ ] Installed executable version matches expected version
- [ ] Installed application normal and relevant failure/recovery journeys pass against the exact installed executable
- [ ] Every install attempts real-audio transcription through the installed real provider; a nonempty transcript is required to mark it passed (record-only and picker-open checks do not count)
- [ ] Installed application launches normally outside the UI driver with a responsive window and live process
- [ ] Screenshots, UIA trees, result files, and failure evidence inspected and locations reported
- [ ] Failed, stalled, untested, or blocked journeys explicitly reported as not fully validated