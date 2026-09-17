# Local installation checklist

- [ ] Source changes and repository status reviewed
- [ ] Solution build succeeds
- [ ] Relevant real Debug application journeys pass
- [ ] Self-contained Release publish succeeds
- [ ] Inno Setup compilation succeeds
- [ ] Installer SHA-256 generated
- [ ] Every `TalkToMe.App` process terminated before installer launch
- [ ] A second process query confirms no instance remains
- [ ] Silent installer exits with code 0
- [ ] Install log retained under `artifacts/validation`
- [ ] Installed executable exists in `%LOCALAPPDATA%\Programs\TalkToMe`
- [ ] Installed executable version matches expected version
- [ ] Installed application launches successfully
- [ ] Relevant live installed-application journeys pass
- [ ] Evidence inspected and locations reported
- [ ] Untested or blocked journeys explicitly reported