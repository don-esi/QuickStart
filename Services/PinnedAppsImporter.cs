using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using QuickStart.Models;

namespace QuickStart.Services;

/// <summary>
/// Imports shortcuts from the classic taskbar "User Pinned\TaskBar" folder.
/// Modern Win11 pin metadata is not fully documented; this covers the .lnk pins that still live there.
/// </summary>
public static class PinnedAppsImporter
{
    public static IReadOnlyList<MenuItemEntry> ImportTaskbarPins()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");

        if (!Directory.Exists(folder))
            return [];

        var results = new List<MenuItemEntry>();
        foreach (var lnk in Directory.EnumerateFiles(folder, "*.lnk"))
        {
            try
            {
                var resolved = ShortcutResolver.Resolve(lnk);
                if (resolved is null)
                    continue;

                results.Add(new MenuItemEntry
                {
                    Name = Path.GetFileNameWithoutExtension(lnk),
                    Path = resolved.TargetPath,
                    Arguments = resolved.Arguments ?? string.Empty,
                    WorkingDirectory = resolved.WorkingDirectory ?? string.Empty
                });
            }
            catch
            {
                // Skip unreadable shortcuts.
            }
        }

        return results;
    }
}

public sealed record ResolvedShortcut(string TargetPath, string? Arguments, string? WorkingDirectory);

public static class ShortcutResolver
{
    public static ResolvedShortcut? Resolve(string shortcutPath)
    {
        var link = (IShellLinkW)new ShellLink();
        try
        {
            ((IPersistFile)link).Load(shortcutPath, 0);

            var target = new StringBuilder(260);
            var data = new WIN32_FIND_DATAW();
            link.GetPath(target, target.Capacity, ref data, 0);

            var args = new StringBuilder(1024);
            link.GetArguments(args, args.Capacity);

            var dir = new StringBuilder(260);
            link.GetWorkingDirectory(dir, dir.Capacity);

            var path = target.ToString();
            if (string.IsNullOrWhiteSpace(path))
                return null;

            return new ResolvedShortcut(
                path,
                string.IsNullOrWhiteSpace(args.ToString()) ? null : args.ToString(),
                string.IsNullOrWhiteSpace(dir.ToString()) ? null : dir.ToString());
        }
        finally
        {
            Marshal.ReleaseComObject(link);
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink;

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, ref WIN32_FIND_DATAW pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WIN32_FIND_DATAW
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }
}
