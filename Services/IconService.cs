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
    /// Build a tray icon with correct HICON ownership.
    /// Bitmap.GetHicon() requires DestroyIcon; Icon.FromHandle does not take ownership.
    /// </summary>
    public static Icon CreateTrayIcon()
    {
        try
        {
            using var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var brush = new SolidBrush(System.Drawing.Color.FromArgb(255, 32, 32, 32));
                g.FillRectangle(brush, 1, 1, 14, 14);
                using var accent = new SolidBrush(System.Drawing.Color.FromArgb(255, 220, 220, 220));
                g.FillRectangle(accent, 3, 4, 10, 2);
                g.FillRectangle(accent, 3, 7, 10, 2);
                g.FillRectangle(accent, 3, 10, 7, 2);
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
        catch (Exception ex)
        {
            AppLog.Error("CreateTrayIcon failed — falling back to SystemIcons.Application", ex);
            return (Icon)SystemIcons.Application.Clone();
        }
    }
}
