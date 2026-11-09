using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QuickStartMenu.Services;

public static class IconService
{
    public static ImageSource? GetIcon(string? path, int size = 16)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
            if (!File.Exists(expanded) && !Directory.Exists(expanded))
                return null;

            using var icon = Icon.ExtractAssociatedIcon(expanded);
            if (icon is null)
                return null;

            var source = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(size, size));
            source.Freeze();
            return source;
        }
        catch
        {
            return null;
        }
    }

    public static Icon CreateTrayIcon()
    {
        var bmp = new Bitmap(16, 16);
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

        return Icon.FromHandle(bmp.GetHicon());
    }
}
