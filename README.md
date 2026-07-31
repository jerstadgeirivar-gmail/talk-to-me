# TalkToMe

TalkToMe (Snakk til meg! - Harry Hole), is a Windows 11 WPF utility for recording Norwegian dictation, transcribing locally by default, and inserting the completed text into the originally focused application. Azure OpenAI remains optional; LM Studio and Ollama profiles report their current speech-to-text limitation honestly.

Transcription runs locally on your computer by default. You can optionally connect TalkToMe to an Azure OpenAI transcription deployment.

## Install

1. Open the repository's [latest release](https://github.com/jerstadgeirivar-gmail/talk-to-me/releases/latest).
2. Download `TalkToMe-Setup.exe` and the accompanying `.sha256` file.
3. Optionally verify the download in PowerShell:

   ```powershell
   Get-FileHash .\TalkToMe-Setup.exe -Algorithm SHA256
   ```

   Confirm that the displayed hash matches the value in the downloaded `.sha256` file.

4. Run `TalkToMe-Setup.exe`.

The application is currently unsigned, so Windows may display a Microsoft Defender SmartScreen warning. Confirm that the installer came from this repository before choosing to run it.

TalkToMe installs for the current Windows user and starts minimized in the system tray. It does not require administrator access.

## First start

TalkToMe uses Local Whisper by default. The speech model is not bundled with the installer, so on first start the app asks permission to download approximately 190 MB. The model is verified before installation and stored in `%LOCALAPPDATA%\TalkToMe\Models`.

Allow roughly 500 MB of free disk space and 1.5 GB of available memory. Transcription speed depends on your CPU.

## Dictate text

1. Place the cursor in the application where the text should appear.
2. Press `Win+<` to start recording.
3. Speak your dictation.
4. Press `Win+<` again to stop recording.
5. Wait for transcription. TalkToMe returns focus to the original window and inserts the completed text.

You can also open TalkToMe from its system-tray icon and use the on-screen recording controls. If automatic insertion is unavailable, copy the completed transcript from the main window.

The shortcut can be changed under **Settings → Dictation preferences**.

## Voice command

TalkToMe can listen locally for the English keyword **computer**:

- Say **computer** once to start recording.
- Say **computer** again to stop and transcribe.

Voice-command listening is disabled by default. Enable it under **Settings → Dictation preferences**. Keyword recognition happens locally.

## Choose a transcription provider

Open **Settings → Transcription**.

- **Local Whisper** is the default and keeps audio on this computer.
- **Azure OpenAI** sends completed recordings to the Azure endpoint you configure. The API key is protected for the current Windows user with DPAPI.
- **LM Studio** and **Ollama** profiles can be configured, but their currently supported APIs do not provide speech-to-text, so TalkToMe does not send audio to them.

There is no automatic fallback between providers. Use **Test provider** after changing the configuration. Use **Repair model** if the local Whisper model is missing or damaged.

## Privacy and local data

- Local Whisper audio stays on your computer.
- Azure OpenAI receives audio only when it is the selected provider.
- Completed recordings are deleted after transcription.
- Recordings from failed transcriptions are deleted by default. You can explicitly enable **Keep audio after failed transcription** if you want retry support.
- During automatic insertion, TalkToMe temporarily uses the Windows clipboard and restores its previous contents. If activation or pasting fails, the previous clipboard contents are restored as well.
- Choosing **Copy transcript** intentionally places the transcript on the clipboard until another application replaces it.
- An interrupted session may leave recoverable audio under `%LOCALAPPDATA%\TalkToMe\Pending`; the main window allows you to delete it.
- Settings and protected credentials are stored below `%LOCALAPPDATA%\TalkToMe`.

To remove all local TalkToMe data after uninstalling, delete `%LOCALAPPDATA%\TalkToMe` after confirming that it contains no recording you want to keep.

## Troubleshooting

- If text is not inserted into an administrator-elevated application, run both applications at the same Windows integrity level.
- If the shortcut does not work, choose another shortcut in Settings; another application may already own it.
- If local transcription is unavailable, open Settings and select **Repair model**.
- If Windows blocks the installer, verify that it came from this repository and that its SHA-256 hash matches the release checksum.

See the full [troubleshooting guide](docs/troubleshooting.md) and [configuration reference](docs/configuration.md).

## Developers

Build instructions, validation commands, architecture notes, and release details are in the [development guide](docs/development.md).
