using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QuickStartMenu.Services;

public static class IconService
{
    private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static ImageSource? GetIcon(string? path, int size = 16)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var expanded = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        var key = $"{size}|{expanded}";

        if (Cache.TryGetValue(key, out var cached))
            return cached;

        ImageSource? source = null;
        try
        {
            if (!File.Exists(expanded) && !Directory.Exists(expanded))
            {
                Cache[key] = null;
                return null;
            }

            using var icon = Icon.ExtractAssociatedIcon(expanded);
            if (icon is null)
            {
                Cache[key] = null;
                return null;
            }

            source = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(size, size));
            source.Freeze();
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Icon failed for '{expanded}': {ex.Message}");
            source = null;
        }

        Cache[key] = source;
        return source;
    }

    /// <summary>
    /// Load the multi-size app.ico (preferred) or fall back to a generated glyph.
    /// </summary>
    public static Icon CreateTrayIcon()
    {
        try
        {
            foreach (var candidate in AppIconCandidates())
            {
                if (!File.Exists(candidate))
                    continue;

                // Request 16×16; Windows picks the best image from the .ico.
                var icon = new Icon(candidate, 16, 16);
                AppLog.Info($"Tray icon loaded from {candidate} ({icon.Width}x{icon.Height})");
                return icon;
            }

            AppLog.Warn("app.ico not found beside exe — using drawn fallback");
            return CreateFallbackTrayIcon();
        }
        catch (Exception ex)
        {
            AppLog.Error("CreateTrayIcon failed — falling back to SystemIcons.Application", ex);
            return (Icon)SystemIcons.Application.Clone();
        }
    }

    private static IEnumerable<string> AppIconCandidates()
    {
        var baseDir = AppContext.BaseDirectory;
        yield return Path.Combine(baseDir, "app.ico");
        yield return Path.Combine(baseDir, "Resources", "app.ico");

        // Dev / single-file edge cases
        var process = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(process))
        {
            var dir = Path.GetDirectoryName(process);
            if (!string.IsNullOrEmpty(dir))
            {
                yield return Path.Combine(dir, "app.ico");
                yield return Path.Combine(dir, "Resources", "app.ico");
            }
        }
    }

    private static Icon CreateFallbackTrayIcon()
    {
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(System.Drawing.Color.Transparent);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            using var fill = new SolidBrush(System.Drawing.Color.FromArgb(255, 242, 242, 240));
            using var ink = new SolidBrush(System.Drawing.Color.FromArgb(255, 28, 28, 28));
            using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(255, 28, 28, 28));
            g.FillRectangle(fill, 1, 1, 14, 14);
            g.DrawRectangle(pen, 1, 1, 13, 13);
            g.FillRectangle(ink, 3, 4, 10, 2);
            g.FillRectangle(ink, 3, 7, 10, 2);
            g.FillRectangle(ink, 3, 10, 7, 2);
        }

        var hIcon = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }
}
