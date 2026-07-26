using System.Runtime.InteropServices;
using VoiceType.Core;

namespace VoiceType.Infrastructure;

public sealed class WindowsGlobalHotkeyService : IGlobalHotkeyService
{
    private const uint AltModifier = 0x0001;
    private const uint ControlModifier = 0x0002;
    private const uint NoRepeatModifier = 0x4000;
    private const uint F9VirtualKey = 0x78;
    private nint _windowHandle;

    public int HotkeyId => 0x5654;

    public int WindowMessage => 0x0312;

    public bool Register(nint windowHandle)
    {
        if (_windowHandle != 0)
        {
            throw new InvalidOperationException("The global hotkey is already registered.");
        }

        bool registered = NativeHotkeyMethods.RegisterHotKey(
            windowHandle,
            HotkeyId,
            ControlModifier | AltModifier | NoRepeatModifier,
            F9VirtualKey);
        if (registered)
        {
            _windowHandle = windowHandle;
        }

        return registered;
    }

    public void Unregister()
    {
        if (_windowHandle == 0)
        {
            return;
        }

        NativeHotkeyMethods.UnregisterHotKey(_windowHandle, HotkeyId);
        _windowHandle = 0;
    }

    public void Dispose() => Unregister();
}

internal static partial class NativeHotkeyMethods
{
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterHotKey(nint windowHandle, int id, uint modifiers, uint virtualKey);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterHotKey(nint windowHandle, int id);
}
