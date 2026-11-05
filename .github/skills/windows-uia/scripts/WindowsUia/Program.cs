using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace WindowsUia;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 3 && args[0] == "discover")
            {
                using Timer watchdog = CreateWatchdog(args[1], 60);
                using UIA3Automation automation = new();
                bool includeText = bool.Parse(args[2]);
                object[] windows = automation.GetDesktop().FindAllChildren().Select(window => (object)new
                {
                    processId = Optional(() => window.Properties.ProcessId.Value, 0),
                    automationId = Optional(() => window.AutomationId, string.Empty),
                    className = Optional(() => window.ClassName, string.Empty),
                    name = includeText ? Optional(() => window.Name, string.Empty) : null,
                }).ToArray();
                WriteJson(args[1], "windows.json", windows);
                Console.WriteLine($"PASS discover windows={windows.Length}");
                return 0;
            }
            if (args.Length is 5 or 6 && args[0] == "inspect")
            {
                using Timer watchdog = CreateWatchdog(args[2], 60);
                int processId = int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
                using UIA3Automation automation = new();
                using Application application = Application.Attach(processId);
                Window window = FindWindow(application, automation, args.Length == 6 ? args[5] : null);
                Snapshot(window, args[2], "inspection", bool.Parse(args[3]), bool.Parse(args[4]));
                Console.WriteLine($"PASS inspect pid={processId}");
                return 0;
            }
            if (args.Length is 5 or 6 && args[0] == "run")
                return ScenarioRunner.Run(args[1], args[2], bool.Parse(args[3]), bool.Parse(args[4]),
                    args.Length == 6 ? int.Parse(args[5], System.Globalization.CultureInfo.InvariantCulture) : null);
            throw new ArgumentException("Use discover <evidence> <include-text> or inspect <pid> <evidence> <include-text> <screenshots> [window-id].");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL {exception.GetType().Name}: {exception.Message}");
            return 1;
        }
    }

    internal static Window FindWindow(Application application, UIA3Automation automation, string? automationId)
    {
        Window[] windows = application.GetAllTopLevelWindows(automation)
            .Where(window => window.Properties.ProcessId.Value == application.ProcessId &&
                (string.IsNullOrEmpty(automationId) || window.AutomationId == automationId)).ToArray();
        return windows.Length == 1 ? windows[0]
            : throw new InvalidOperationException($"Expected one window for PID {application.ProcessId}; found {windows.Length}. Supply a unique window automation ID.");
    }

    internal static void Snapshot(Window window, string directory, string stem, bool includeText, bool screenshots)
    {
        List<object> nodes = [];
        Queue<(AutomationElement Element, int Depth)> pending = new();
        pending.Enqueue((window, 0));
        while (pending.Count > 0 && nodes.Count < 2000)
        {
            (AutomationElement element, int depth) = pending.Dequeue();
            bool password = Optional(() => element.Properties.IsPassword.Value, true);
            nodes.Add(new
            {
                depth,
                processId = Optional(() => element.Properties.ProcessId.Value, 0),
                automationId = Optional(() => element.AutomationId, string.Empty),
                controlType = Optional(() => element.ControlType.ToString(), "Unsupported"),
                name = includeText && !password ? Optional(() => element.Name, string.Empty) : null,
                enabled = Optional(() => element.IsEnabled, false),
                offscreen = Optional(() => element.IsOffscreen, true),
                bounds = Optional(() => element.BoundingRectangle.ToString(), "Unsupported"),
            });
            if (depth < 20)
                foreach (AutomationElement child in element.FindAllChildren()) pending.Enqueue((child, depth + 1));
        }
        WriteJson(directory, stem + "-tree.json", new { nodes, truncated = pending.Count > 0 });
        if (screenshots) Capture(window, Path.Combine(directory, stem + ".png"));
    }

    internal static void WriteJson(string directory, string filename, object value)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, filename), JsonSerializer.Serialize(value, JsonOptions));
    }

    internal static Timer CreateWatchdog(string directory, int seconds, Action? cleanup = null) => new(_ =>
    {
        try
        {
            WriteJson(directory, "watchdog.json", new { result = "FAIL", reason = "UIA watchdog expired", seconds });
            cleanup?.Invoke();
        }
        finally { Environment.Exit(124); }
    }, null, TimeSpan.FromSeconds(seconds), Timeout.InfiniteTimeSpan);

    private static T Optional<T>(Func<T> read, T fallback)
    {
        try { return read(); }
        catch (FlaUI.Core.Exceptions.PropertyNotSupportedException) { return fallback; }
    }

    private static void Capture(Window window, string path)
    {
        nint handle = window.Properties.NativeWindowHandle.Value;
        Rectangle bounds = Rectangle.Round(window.BoundingRectangle);
        if (bounds.Width <= 0 || bounds.Height <= 0) throw new InvalidOperationException("Window capture has empty bounds.");
        using Bitmap bitmap = new(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        nint context = graphics.GetHdc();
        try
        {
            if (!NativeMethods.PrintWindow(handle, context, 2)) throw new InvalidOperationException("PrintWindow capture failed.");
        }
        finally { graphics.ReleaseHdc(context); }
        bitmap.Save(path, ImageFormat.Png);
    }
}

internal static partial class NativeMethods
{
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PrintWindow(nint handle, nint deviceContext, uint flags);
}