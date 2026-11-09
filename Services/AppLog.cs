using System.Diagnostics;
using System.IO;
using System.Text;

namespace QuickStart.Services;

/// <summary>
/// Durable log at %AppData%\QuickStart\debug.log (also Debug.WriteLine).
/// Debug: verbose Info/Warn/Error. Release: exceptions with stack traces only.
/// Size-rotated: rolls to debug.log.1 / .2 when over ~2 MB.
/// </summary>
public static class AppLog
{
    private const long MaxBytes = 2L * 1024 * 1024; // ~2 MB active file
    private const int MaxBackups = 2; // debug.log.1, debug.log.2

    private static readonly object Gate = new();
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "QuickStart",
        "debug.log");

    public static string LogFilePath => LogPath;

    [Conditional("DEBUG")]
    public static void Info(string message) => Write("INFO", message);

    [Conditional("DEBUG")]
    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null)
    {
#if !DEBUG
        // Release: exceptions (with stack) only — skip message-only errors.
        if (ex is null)
            return;
        Write("ERROR", $"{message} | {ex}");
#else
        if (ex is null)
            Write("ERROR", message);
        else
            Write("ERROR", $"{message} | {ex}");
#endif
    }

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] [T{Environment.CurrentManagedThreadId}] {message}";
        try
        {
            Debug.WriteLine(line);
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                RotateIfNeeded();
                File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // Never let logging break the app.
        }
    }

    private static void RotateIfNeeded()
    {
        try
        {
            var info = new FileInfo(LogPath);
            if (!info.Exists || info.Length < MaxBytes)
                return;

            // Oldest first: drop .N, then shift .1→.2 … current→.1
            var oldest = $"{LogPath}.{MaxBackups}";
            if (File.Exists(oldest))
                File.Delete(oldest);

            for (var i = MaxBackups - 1; i >= 1; i--)
            {
                var src = $"{LogPath}.{i}";
                var dst = $"{LogPath}.{i + 1}";
                if (File.Exists(src))
                    File.Move(src, dst, overwrite: true);
            }

            File.Move(LogPath, $"{LogPath}.1", overwrite: true);
        }
        catch
        {
            // If rotate fails, keep appending — still better than crashing.
        }
    }
}
