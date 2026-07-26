# ADR 004: Clipboard-Assisted Insertion

Use Unicode clipboard staging plus `SendInput` Ctrl+V for broad Win32/WPF/Electron/browser compatibility. Validate and reactivate the captured HWND/PID, never send Enter, retry clipboard contention, and restore prior data only while TalkToMe still owns the temporary text.
