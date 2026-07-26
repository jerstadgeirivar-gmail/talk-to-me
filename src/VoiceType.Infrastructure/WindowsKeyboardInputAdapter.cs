using System.ComponentModel;
using System.Runtime.InteropServices;

namespace VoiceType.Infrastructure;

public sealed class WindowsKeyboardInputAdapter : IKeyboardInputAdapter
{
    private const ushort ControlKey = 0x11;
    private const ushort VKey = 0x56;

    public void Paste()
    {
        NativeInputMethods.Input[] inputs =
        [
            NativeInputMethods.CreateKeyboardInput(ControlKey, keyUp: false),
            NativeInputMethods.CreateKeyboardInput(VKey, keyUp: false),
            NativeInputMethods.CreateKeyboardInput(VKey, keyUp: true),
            NativeInputMethods.CreateKeyboardInput(ControlKey, keyUp: true),
        ];
        uint inserted = NativeInputMethods.SendInput(
            checked((uint)inputs.Length),
            inputs,
            Marshal.SizeOf<NativeInputMethods.Input>());
        if (inserted != inputs.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows did not accept the paste input sequence.");
        }
    }
}

internal static partial class NativeInputMethods
{
    private const uint KeyboardInputType = 1;
    private const uint KeyUpFlag = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    internal struct Input
    {
        internal uint Type;
        internal InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)]
        internal MouseInput Mouse;

        [FieldOffset(0)]
        internal KeyboardInput Keyboard;

        [FieldOffset(0)]
        internal HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseInput
    {
        internal int X;
        internal int Y;
        internal uint MouseData;
        internal uint Flags;
        internal uint Time;
        internal nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardInput
    {
        internal ushort VirtualKey;
        internal ushort ScanCode;
        internal uint Flags;
        internal uint Time;
        internal nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HardwareInput
    {
        internal uint Message;
        internal ushort ParameterLow;
        internal ushort ParameterHigh;
    }

    internal static Input CreateKeyboardInput(ushort virtualKey, bool keyUp) => new()
    {
        Type = KeyboardInputType,
        Data = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = virtualKey,
                Flags = keyUp ? KeyUpFlag : 0,
            },
        },
    };

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial uint SendInput(uint inputCount, [In] Input[] inputs, int inputSize);
}
