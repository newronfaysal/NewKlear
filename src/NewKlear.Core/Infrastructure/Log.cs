namespace NewKlear.Core.Infrastructure;

/// <summary>Simple thread-safe session log. One file per app session.</summary>
public static class Log
{
    private static readonly object Gate = new();
    private static string? _file;

    public static string CurrentFile
    {
        get
        {
            lock (Gate)
            {
                _file ??= Path.Combine(AppPaths.Logs, $"session-{DateTime.Now:yyyyMMdd-HHmmss}.log");
                return _file;
            }
        }
    }

    public static event Action<string>? LineWritten;

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}: {ex.GetType().Name}: {ex.Message}");

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
        try
        {
            var file = CurrentFile;
            lock (Gate) File.AppendAllText(file, line + Environment.NewLine);
        }
        catch
        {
            // Logging must never crash the app.
        }
        LineWritten?.Invoke(line);
    }
}
