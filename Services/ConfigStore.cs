using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using QuickStart.Models;

namespace QuickStart.Services;

public sealed class ConfigStore
{
    private const string AppFolderName = "QuickStart";
    private const string LegacyAppFolderName = "QuickStartMenu";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _configPath;

    public ConfigStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, AppFolderName);
        Directory.CreateDirectory(dir);
        _configPath = Path.Combine(dir, "menu.json");
        MigrateFromLegacy(appData, dir);
    }

    public string ConfigPath => _configPath;

    public MenuConfig Load()
    {
        if (!File.Exists(_configPath))
        {
            AppLog.Info($"Config missing — writing defaults to {_configPath}");
            var defaults = MenuConfig.CreateDefault();
            Save(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<MenuConfig>(json, JsonOptions) ?? MenuConfig.CreateDefault();
            AppLog.Info($"Config loaded from {_configPath} ({json.Length} bytes)");
            return config;
        }
        catch (Exception ex)
        {
            AppLog.Error("Config load failed — using defaults", ex);
            return MenuConfig.CreateDefault();
        }
    }

    public void Save(MenuConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(_configPath, json);
        AppLog.Info($"Config saved ({json.Length} bytes)");
    }

    /// <summary>
    /// One-time copy of menu.json (and log leftovers) from %AppData%\QuickStartMenu.
    /// </summary>
    private static void MigrateFromLegacy(string appData, string newDir)
    {
        try
        {
            var legacyDir = Path.Combine(appData, LegacyAppFolderName);
            if (!Directory.Exists(legacyDir))
                return;

            var legacyMenu = Path.Combine(legacyDir, "menu.json");
            var newMenu = Path.Combine(newDir, "menu.json");
            if (File.Exists(legacyMenu) && !File.Exists(newMenu))
            {
                File.Copy(legacyMenu, newMenu);
                AppLog.Info($"Migrated menu.json from {legacyDir} -> {newDir}");
            }

            // Bring along rotated logs only if the new log is absent.
            var newLog = Path.Combine(newDir, "debug.log");
            if (!File.Exists(newLog))
            {
                foreach (var name in new[] { "debug.log", "debug.log.1", "debug.log.2" })
                {
                    var src = Path.Combine(legacyDir, name);
                    var dst = Path.Combine(newDir, name);
                    if (File.Exists(src) && !File.Exists(dst))
                        File.Copy(src, dst);
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Legacy AppData migrate skipped: {ex.Message}");
        }
    }
}
