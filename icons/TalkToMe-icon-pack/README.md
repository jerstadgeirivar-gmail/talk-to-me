# TalkToMe icon pack

The icon combines a microphone with a green text caret to represent TalkToMe's
core action: converting speech into text at the user's current cursor position.

## Primary files

- `TalkToMe.ico` — multi-resolution Windows icon for the executable, window,
  and system tray.
- `TalkToMe.png` — 512×512 transparent PNG master for other uses.
- `png/` — individual transparent PNG files from 16×16 through 512×512.

## Implementation handoff

Copy `TalkToMe.ico` into the repository, preferably under
`Assets/Branding/TalkToMe.ico`, and configure it as a content/resource file as
required by the existing C# UI framework.

Use the same `.ico` file for:

1. the built executable/application icon;
2. the main window icon;
3. the system tray/notification icon.

Replace all references to the generic .NET/default icon. Preserve the existing
tray behavior and application logic.

Verify the result in the Windows taskbar, Alt+Tab view, title bar, executable,
and notification area at 100% and 150% display scaling.

## Colors

- Navy: approximately `#102231`
- Emerald green: approximately `#08784E`
- White: `#FFFFFF`

The transparent source and original generated master are included in `source/`.
