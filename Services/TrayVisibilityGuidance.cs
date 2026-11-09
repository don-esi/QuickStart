using System.Diagnostics;

namespace QuickStartMenu.Services;

/// <summary>
/// First-run / help UX when Windows hides the icon in the notification overflow.
/// </summary>
public static class TrayVisibilityGuidance
{
    public const string SettingsUri = "ms-settings:taskbar";

    public static string ManualSteps { get; } =
        "Windows hides new tray icons in the overflow (^) by default.\n\n" +
        "To keep Quick Start Menu always visible:\n" +
        "1. Open Settings → Personalization → Taskbar\n" +
        "2. Open “Other system tray icons” (or “Select which icons appear on the taskbar”)\n" +
        "3. Turn on Quick Start Menu / QuickStartMenu.exe\n\n" +
        "On Windows 11 we also try to promote the icon automatically when Windows allows it.";

    public static void OpenTaskbarSettings()
    {
        try
        {
            AppLog.Info("Opening taskbar settings URI");
            Process.Start(new ProcessStartInfo
            {
                FileName = SettingsUri,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppLog.Error("OpenTaskbarSettings failed", ex);
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "ms-settings:taskbar",
                    UseShellExecute = true
                });
            }
            catch (Exception ex2)
            {
                AppLog.Error("OpenTaskbarSettings fallback failed", ex2);
            }
        }
    }
}
