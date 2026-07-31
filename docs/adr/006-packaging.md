# ADR 006: Packaging

Publish a self-contained `win-x64` directory and wrap it with a per-user Inno Setup installer. The model is deliberately excluded so the installer remains practical to distribute. Whisper.net and its native CPU runtime remain self-contained in the application.

On first startup with Local Whisper selected and no valid model, the application asks for consent to download the pinned 190,085,487-byte multilingual model. It shows progress and cancellation, verifies the pinned size and SHA-256, and atomically moves it to `%LOCALAPPDATA%\TalkToMe\Models`. Declining leaves the application open and points the user to Settings. Upgrades and uninstall preserve the separately downloaded model to avoid needless downloads; the user may delete it manually. Whisper.net/whisper.cpp/OpenAI Whisper attributions ship under `{app}\licenses`. Installation remains non-elevated. The current output is unsigned and can trigger SmartScreen.
