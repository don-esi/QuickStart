using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using QuickStartMenu.Models;

namespace QuickStartMenu.Services;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _configPath;

    public ConfigStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "QuickStartMenu");
        Directory.CreateDirectory(dir);
        _configPath = Path.Combine(dir, "menu.json");
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
}
