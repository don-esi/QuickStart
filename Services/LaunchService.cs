using System.Diagnostics;
using System.IO;

namespace QuickStart.Services;

public static class LaunchService
{
    public static void Launch(string path, string? arguments = null, string? workingDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is empty.", nameof(path));

        var expanded = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        var ext = Path.GetExtension(expanded).ToLowerInvariant();

        var psi = new ProcessStartInfo
        {
            UseShellExecute = true,
            WorkingDirectory = ResolveWorkingDirectory(expanded, workingDirectory)
        };

        switch (ext)
        {
            case ".ps1":
                psi.FileName = "powershell.exe";
                psi.Arguments = BuildPowerShellArgs(expanded, arguments);
                break;
            case ".cmd":
            case ".bat":
                psi.FileName = "cmd.exe";
                psi.Arguments = $"/c \"{expanded}\"{(string.IsNullOrWhiteSpace(arguments) ? "" : " " + arguments)}";
                break;
            default:
                psi.FileName = expanded;
                if (!string.IsNullOrWhiteSpace(arguments))
                    psi.Arguments = arguments;
                break;
        }

        AppLog.Info($"LaunchService Start file={psi.FileName} args={psi.Arguments} cwd={psi.WorkingDirectory}");
        Process.Start(psi);
    }

    private static string BuildPowerShellArgs(string scriptPath, string? extraArgs)
    {
        var args = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"";
        if (!string.IsNullOrWhiteSpace(extraArgs))
            args += " " + extraArgs;
        return args;
    }

    private static string ResolveWorkingDirectory(string path, string? workingDirectory)
    {
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            var wd = Environment.ExpandEnvironmentVariables(workingDirectory);
            if (Directory.Exists(wd))
                return wd;
        }

        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                return dir;
        }
        catch
        {
            // ignore
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
