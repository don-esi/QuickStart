using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace QuickStart.Services;

/// <summary>
/// WinForms / Win32 screen coords are physical pixels; WPF Left/Top are DIPs.
/// Mixing them on a scaled display places the flyout off-screen.
/// </summary>
public static class DpiHelper
{
    public static Matrix GetTransformFromDevice(Window window)
    {
        // Ensure HWND exists so CompositionTarget is available before Show().
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var source = HwndSource.FromHwnd(hwnd);
        if (source?.CompositionTarget is not null)
            return source.CompositionTarget.TransformFromDevice;

        AppLog.Warn("DpiHelper: no CompositionTarget — assuming 96 DPI");
        return Matrix.Identity;
    }

    public static Point PhysicalToDip(Window window, double physicalX, double physicalY)
    {
        var m = GetTransformFromDevice(window);
        return m.Transform(new Point(physicalX, physicalY));
    }

    public static Rect PhysicalToDip(Window window, System.Drawing.Rectangle physical)
    {
        var topLeft = PhysicalToDip(window, physical.Left, physical.Top);
        var bottomRight = PhysicalToDip(window, physical.Right, physical.Bottom);
        return new Rect(topLeft, bottomRight);
    }
}
