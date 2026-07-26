using System.Runtime.InteropServices;
using TalkToMe.Core;

namespace TalkToMe.Infrastructure;

public sealed class WindowsGlobalHotkeyService
    : IGlobalHotkeyService
{
    private const uint NoRepeatModifier = 0x4000;
    private HotkeyGesture _gesture;
    private nint _windowHandle;

    public WindowsGlobalHotkeyService(string hotkey)
    {
        _gesture = HotkeyGesture.Parse(hotkey);
        GestureText = hotkey;
    }

    public string GestureText { get; private set; }

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
            _gesture.Modifiers | NoRepeatModifier,
            _gesture.VirtualKey);
        if (registered)
        {
            _windowHandle = windowHandle;
        }

        return registered;
    }

    public bool Reconfigure(string hotkey)
    {
        HotkeyGesture newGesture = HotkeyGesture.Parse(hotkey);
        if (_windowHandle == 0)
        {
            _gesture = newGesture;
            GestureText = hotkey;
            return true;
        }

        nint windowHandle = _windowHandle;
        HotkeyGesture previousGesture = _gesture;
        string previousText = GestureText;
        Unregister();
        _gesture = newGesture;
        GestureText = hotkey;
        if (Register(windowHandle))
        {
            return true;
        }

        _gesture = previousGesture;
        GestureText = previousText;
        Register(windowHandle);
        return false;
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

public readonly record struct HotkeyGesture(uint Modifiers, uint VirtualKey)
{
    private const uint AltModifier = 0x0001;
    private const uint ControlModifier = 0x0002;
    private const uint ShiftModifier = 0x0004;
    private const uint WindowsModifier = 0x0008;
    private const uint Oem102VirtualKey = 0xE2;

    public static bool TryParse(string? value, out HotkeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] parts = value.Split(
            '+',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        uint modifiers = 0;
        foreach (string modifier in parts[..^1])
        {
            uint flag = modifier.ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => ControlModifier,
                "ALT" => AltModifier,
                "SHIFT" => ShiftModifier,
                "WIN" or "WINDOWS" => WindowsModifier,
                _ => 0,
            };
            if (flag == 0 || (modifiers & flag) != 0)
            {
                return false;
            }

            modifiers |= flag;
        }

        if (modifiers == 0 || !TryParseKey(parts[^1], out uint virtualKey))
        {
            return false;
        }

        gesture = new HotkeyGesture(modifiers, virtualKey);
        return true;
    }

    public static HotkeyGesture Parse(string value) =>
        TryParse(value, out HotkeyGesture gesture)
            ? gesture
            : throw new FormatException($"Invalid global hotkey: '{value}'.");

    private static bool TryParseKey(string key, out uint virtualKey)
    {
        string normalized = key.Trim().ToUpperInvariant();
        if (normalized is "<" or "OEM102" or "OEM_102")
        {
            virtualKey = Oem102VirtualKey;
            return true;
        }

        if (normalized.Length == 1 &&
            ((normalized[0] >= 'A' && normalized[0] <= 'Z') ||
             (normalized[0] >= '0' && normalized[0] <= '9')))
        {
            virtualKey = normalized[0];
            return true;
        }

        if (normalized.Length is 2 or 3 &&
            normalized[0] == 'F' &&
            int.TryParse(normalized[1..], out int functionKey) &&
            functionKey is >= 1 and <= 24)
        {
            virtualKey = (uint)(0x70 + functionKey - 1);
            return true;
        }

        virtualKey = 0;
        return false;
    }
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
