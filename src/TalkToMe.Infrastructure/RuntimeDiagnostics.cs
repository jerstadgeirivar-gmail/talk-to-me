using System.Text.Json;

namespace TalkToMe.Infrastructure;

public static class RuntimeDiagnostics
{
    private static readonly object Gate = new();
    private static string _directory = TalkToMeDataPaths.DiagnosticsDirectory;
    private static bool _enabled = true;

    public static string LogPath => Path.Combine(_directory, $"runtime-{DateTime.UtcNow:yyyyMMdd}-{Environment.ProcessId}.jsonl");

    public static void Configure(string? directory, string? level)
    {
        _directory = directory ?? TalkToMeDataPaths.DiagnosticsDirectory;
        _enabled = !string.Equals(level, "None", StringComparison.OrdinalIgnoreCase);
    }

    public static void SetLevel(string? level) =>
        _enabled = !string.Equals(level, "None", StringComparison.OrdinalIgnoreCase);

    public static void Write(string eventName, object? details = null)
    {
        if (!_enabled) return;
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(_directory);
                foreach (string oldFile in Directory.EnumerateFiles(_directory, "runtime-*.jsonl"))
                {
                    if (File.GetLastWriteTimeUtc(oldFile) < DateTime.UtcNow.AddDays(-14)) File.Delete(oldFile);
                }
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length >= 10 * 1024 * 1024) return;
                string entry = JsonSerializer.Serialize(new
                {
                    timestamp = DateTimeOffset.UtcNow,
                    processId = Environment.ProcessId,
                    eventName,
                    details,
                });
                File.AppendAllText(LogPath, entry + Environment.NewLine);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
        }
    }

    public static void Failure(string eventName, Exception exception) => Write(eventName, new
    {
        exceptionType = exception.GetType().FullName,
        exception.Message,
        exception.HResult,
        exception.StackTrace,
        innerType = exception.InnerException?.GetType().FullName,
        innerMessage = exception.InnerException?.Message,
        category = (exception as TalkToMe.Core.TranscriptionException)?.Category.ToString(),
        statusCode = (exception as TalkToMe.Core.TranscriptionException)?.StatusCode,
    });
}