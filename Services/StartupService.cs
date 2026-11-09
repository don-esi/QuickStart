using Microsoft.Win32;
using System.Diagnostics;
using System.IO;

namespace QuickStart.Services;

public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "QuickStart";
    private const string LegacyValueName = "QuickStartMenu";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        if (key is null)
            return false;

        if (key.GetValue(ValueName) is string)
            return true;

        // Legacy name still counts as enabled until MigrateLegacyRunValue rewrites it.
        return key.GetValue(LegacyValueName) is string;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        // Always clear the old value name so uninstall/disable doesn't leave a stale entry.
        key.DeleteValue(LegacyValueName, throwOnMissingValue: false);

        if (enabled)
        {
            var exe = Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? Path.Combine(AppContext.BaseDirectory, "QuickStart.exe");
            key.SetValue(ValueName, $"\"{exe}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    /// <summary>
    /// If HKCU Run still has QuickStartMenu, rewrite to QuickStart pointing at this exe.
    /// </summary>
    public static void MigrateLegacyRunValue()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key is null)
                return;

            if (key.GetValue(LegacyValueName) is not string)
                return;

            var exe = Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? Path.Combine(AppContext.BaseDirectory, "QuickStart.exe");
            key.SetValue(ValueName, $"\"{exe}\"");
            key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
            AppLog.Info("Migrated HKCU Run QuickStartMenu -> QuickStart");
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Legacy Run key migrate skipped: {ex.Message}");
        }
    }
}
