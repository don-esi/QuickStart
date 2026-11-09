using QuickStartMenu.Models;

namespace QuickStartMenu.Services;

public sealed class MenuController
{
    private readonly ConfigStore _store;
    private MenuConfig _config;

    public MenuController(ConfigStore store)
    {
        _store = store;
        _config = store.Load();
        StartupService.SetEnabled(_config.StartWithWindows);
    }

    public MenuConfig Config => _config;
    public string ConfigPath => _store.ConfigPath;

    public event EventHandler? Changed;

    public void Reload()
    {
        _config = _store.Load();
        RaiseChanged();
    }

    public void Save()
    {
        _store.Save(_config);
        RaiseChanged();
    }

    public void Replace(MenuConfig config)
    {
        _config = config;
        Save();
    }

    public void SetStartWithWindows(bool enabled)
    {
        _config.StartWithWindows = enabled;
        StartupService.SetEnabled(enabled);
        Save();
    }

    public void AddItem(MenuItemEntry item, string? folderId = null)
    {
        if (folderId is null)
        {
            _config.RootItems.Add(item);
        }
        else
        {
            var folder = _config.Folders.FirstOrDefault(f => f.Id == folderId);
            folder?.Items.Add(item);
        }

        Save();
    }

    public void AddFolder(string name)
    {
        _config.Folders.Add(new MenuFolder { Name = name });
        Save();
    }

    public int ImportPinnedApps(string? folderId = null)
    {
        var pins = PinnedAppsImporter.ImportTaskbarPins();
        if (pins.Count == 0)
            return 0;

        var existing = new HashSet<string>(
            _config.RootItems.Select(i => i.Path)
                .Concat(_config.Folders.SelectMany(f => f.Items.Select(i => i.Path))),
            StringComparer.OrdinalIgnoreCase);

        var added = 0;
        MenuFolder? folder = null;
        if (folderId is not null)
            folder = _config.Folders.FirstOrDefault(f => f.Id == folderId);

        foreach (var pin in pins)
        {
            if (existing.Contains(pin.Path))
                continue;

            if (folder is not null)
                folder.Items.Add(pin);
            else
                _config.RootItems.Add(pin);

            existing.Add(pin.Path);
            added++;
        }

        if (added > 0)
            Save();

        return added;
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
