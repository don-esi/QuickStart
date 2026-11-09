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

    private static ImageSource? _appWindowIcon;

    /// <summary>
    /// WPF Window.Icon / Alt-Tab image from the multi-size app.ico (Start-grid).
    /// </summary>
    public static ImageSource? GetAppWindowIcon()
    {
        if (_appWindowIcon is not null)
            return _appWindowIcon;

        try
        {
            foreach (var candidate in AppIconCandidates())
            {
                if (!File.Exists(candidate))
                    continue;

                // BitmapFrame keeps the .ico frames so WPF/Windows can pick a size.
                var frame = BitmapFrame.Create(
                    new Uri(candidate, UriKind.Absolute),
                    BitmapCreateOptions.None,
                    BitmapCacheOption.OnLoad);
                frame.Freeze();
                _appWindowIcon = frame;
                AppLog.Info($"Window icon loaded from {candidate}");
                return _appWindowIcon;
            }

            AppLog.Warn("app.ico not found for Window.Icon");
        }
        catch (Exception ex)
        {
            AppLog.Warn($"GetAppWindowIcon failed: {ex.Message}");
        }

        return null;
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
        // Mini Start-grid (4 blue tiles) so tray still matches if .ico is missing.
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(System.Drawing.Color.Transparent);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            using var light = new SolidBrush(System.Drawing.Color.FromArgb(255, 0x3C, 0xBE, 0xF5));
            using var dark = new SolidBrush(System.Drawing.Color.FromArgb(255, 0x00, 0x78, 0xD4));
            g.FillRectangle(light, 1, 1, 6, 6);
            g.FillRectangle(light, 9, 1, 6, 6);
            g.FillRectangle(dark, 1, 9, 6, 6);
            g.FillRectangle(dark, 9, 9, 6, 6);
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
