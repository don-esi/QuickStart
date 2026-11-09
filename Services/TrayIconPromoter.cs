using System.IO;
using Microsoft.Win32;

namespace QuickStart.Services;

/// <summary>
/// Best-effort tray visibility on Windows 11.
/// Official docs: icons go to overflow by default; only the user can promote them.
/// Win11 22H2+ also stores undocumented HKCU\Control Panel\NotifyIconSettings\&lt;id&gt;\IsPromoted.
/// We set IsPromoted=1 when safe; never override an explicit IsPromoted=0 (user hid us).
/// </summary>
internal static class TrayIconPromoter
{
    private const string KeyPath = @"Control Panel\NotifyIconSettings";
    private const int MinWin11Build = 22000;

    public static bool IsWindows11OrLater =>
        Environment.OSVersion.Version.Build >= MinWin11Build;

    public static string? CurrentExePath
    {
        get
        {
            try
            {
                var path = Environment.ProcessPath;
                return string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
            }
            catch
            {
                return Environment.ProcessPath;
            }
        }
    }

    public static int SweepStaleEntries()
    {
        if (!IsWindows11OrLater)
            return 0;

        var exePath = CurrentExePath;
        var exeName = Path.GetFileName(exePath);
        if (string.IsNullOrWhiteSpace(exeName) || string.IsNullOrWhiteSpace(exePath))
            return 0;

        var swept = 0;
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
            if (root is null)
                return 0;

            foreach (var subName in root.GetSubKeyNames())
            {
                try
                {
                    string? path;
                    using (var sub = root.OpenSubKey(subName, writable: false))
                    {
                        if (sub is null)
                            continue;
                        path = sub.GetValue("ExecutablePath") as string;
                    }

                    if (string.IsNullOrEmpty(path))
                        continue;
                    if (!string.Equals(Path.GetFileName(path), exeName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string pathNormalized;
                    try { pathNormalized = Path.GetFullPath(path); }
                    catch { pathNormalized = path; }

                    if (string.Equals(pathNormalized, exePath, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (File.Exists(path))
                        continue;

                    root.DeleteSubKeyTree(subName, throwOnMissingSubKey: false);
                    AppLog.Info($"TrayIconPromoter: swept stale {subName} → {path}");
                    swept++;
                }
                catch (Exception ex)
                {
                    AppLog.Warn($"TrayIconPromoter sweep {subName}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"TrayIconPromoter.SweepStaleEntries: {ex.Message}");
        }

        return swept;
    }

    /// <summary>Snapshot subkey names before NotifyIcon becomes visible.</summary>
    public static HashSet<string>? CaptureBaseline()
    {
        if (!IsWindows11OrLater)
            return null;

        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false);
            if (root is null)
                return new HashSet<string>(StringComparer.Ordinal);
            return new HashSet<string>(root.GetSubKeyNames(), StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            AppLog.Warn($"TrayIconPromoter.CaptureBaseline: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Returns true once our NotifyIconSettings entry is identified (promoted or user-hidden).
    /// Returns false while waiting for Explorer to create/populate the key — caller retries.
    /// </summary>
    public static bool TryPromote(HashSet<string>? baseline)
    {
        if (!IsWindows11OrLater)
            return false;

        var exePath = CurrentExePath;
        if (string.IsNullOrWhiteSpace(exePath))
            return false;

        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
            if (root is null)
                return false;

            var identified = false;
            var orphans = new List<string>();

            foreach (var subName in root.GetSubKeyNames())
            {
                try
                {
                    using var sub = root.OpenSubKey(subName, writable: true);
                    if (sub is null)
                        continue;

                    var path = sub.GetValue("ExecutablePath") as string;
                    if (!string.IsNullOrEmpty(path))
                    {
                        if (!PathsEqual(path, exePath))
                            continue;

                        identified = true;
                        ApplyPromote(sub, subName, path);
                        continue;
                    }

                    if (baseline is null || baseline.Contains(subName))
                        continue;
                    if (sub.GetValue("IconSnapshot") is not byte[])
                        continue;
                    orphans.Add(subName);
                }
                catch (Exception ex)
                {
                    AppLog.Warn($"TrayIconPromoter subkey {subName}: {ex.Message}");
                }
            }

            if (!identified && orphans.Count == 1)
            {
                var subName = orphans[0];
                try
                {
                    using var sub = root.OpenSubKey(subName, writable: true);
                    if (sub is not null)
                    {
                        if (sub.GetValue("IsPromoted") is int hidden && hidden == 0)
                        {
                            AppLog.Info($"TrayIconPromoter: orphan {subName} IsPromoted=0 — respecting user");
                        }
                        else
                        {
                            sub.SetValue("ExecutablePath", exePath, RegistryValueKind.String);
                            sub.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
                            AppLog.Info($"TrayIconPromoter: claimed orphan {subName} + IsPromoted=1");
                        }

                        identified = true;
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Warn($"TrayIconPromoter claim orphan {subName}: {ex.Message}");
                }
            }
            else if (orphans.Count > 1)
            {
                AppLog.Info($"TrayIconPromoter: {orphans.Count} new orphans — defer retry");
            }

            return identified;
        }
        catch (Exception ex)
        {
            AppLog.Warn($"TrayIconPromoter.TryPromote: {ex.Message}");
            return false;
        }
    }

    private static void ApplyPromote(RegistryKey sub, string subName, string path)
    {
        var current = sub.GetValue("IsPromoted");
        if (current is int hidden && hidden == 0)
        {
            AppLog.Info($"TrayIconPromoter: {subName} IsPromoted=0 — respecting user");
            return;
        }

        if (current is int shown && shown == 1)
            return;

        sub.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
        AppLog.Info($"TrayIconPromoter: promoted {subName} for {path}");
    }

    private static bool PathsEqual(string a, string b)
    {
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
