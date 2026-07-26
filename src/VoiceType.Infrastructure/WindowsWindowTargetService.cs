using System.Diagnostics;
using System.Runtime.InteropServices;
using VoiceType.Core;

namespace VoiceType.Infrastructure;

public sealed class WindowsWindowTargetService(int applicationProcessId) : IWindowTargetService
{
    private static readonly TimeSpan ActivationTimeout = TimeSpan.FromSeconds(2);

    public WindowTarget CaptureForegroundTarget()
    {
        nint handle = NativeWindowMethods.GetForegroundWindow();
        if (handle == 0 || !NativeWindowMethods.IsWindow(handle))
        {
            throw new InvalidOperationException("No valid foreground target window is available.");
        }

        NativeWindowMethods.GetWindowThreadProcessId(handle, out uint processId);
        if (processId == applicationProcessId)
        {
            throw new InvalidOperationException("VoiceType cannot use its own window as the insertion target.");
        }

        return new WindowTarget(handle, checked((int)processId), ReadWindowTitle(handle));
    }

    public bool IsValid(WindowTarget target)
    {
        if (!NativeWindowMethods.IsWindow(target.Handle))
        {
            return false;
        }

        NativeWindowMethods.GetWindowThreadProcessId(target.Handle, out uint currentProcessId);
        return currentProcessId == target.ProcessId;
    }

    public async Task<bool> ActivateAsync(WindowTarget target, CancellationToken cancellationToken)
    {
        if (!IsValid(target))
        {
            return false;
        }

        if (NativeWindowMethods.IsIconic(target.Handle))
        {
            NativeWindowMethods.ShowWindowAsync(target.Handle, NativeWindowMethods.RestoreWindow);
        }

        NativeWindowMethods.SetForegroundWindow(target.Handle);
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < ActivationTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (NativeWindowMethods.GetForegroundWindow() == target.Handle)
            {
                return true;
            }

            await Task.Delay(50, cancellationToken);
        }

        return false;
    }

    private static unsafe string ReadWindowTitle(nint handle)
    {
        int length = NativeWindowMethods.GetWindowTextLength(handle);
        char[] title = new char[length + 1];
        fixed (char* titlePointer = title)
        {
            int written = NativeWindowMethods.GetWindowText(handle, titlePointer, title.Length);
            return new string(titlePointer, 0, written);
        }
    }
}

internal static partial class NativeWindowMethods
{
    internal const int RestoreWindow = 9;

    [LibraryImport("user32.dll")]
    internal static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindow(nint windowHandle);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsIconic(nint windowHandle);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetForegroundWindow(nint windowHandle);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ShowWindowAsync(nint windowHandle, int command);

    [LibraryImport("user32.dll")]
    internal static partial uint GetWindowThreadProcessId(nint windowHandle, out uint processId);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextLengthW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int GetWindowTextLength(nint windowHandle);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextW", StringMarshalling = StringMarshalling.Utf16)]
    internal static unsafe partial int GetWindowText(nint windowHandle, char* text, int maximumCount);
}
