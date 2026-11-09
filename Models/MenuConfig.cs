namespace QuickStartMenu.Models;

public sealed class MenuConfig
{
    public List<MenuFolder> Folders { get; set; } = [];
    public List<MenuItemEntry> RootItems { get; set; } = [];
    public bool StartWithWindows { get; set; }

    /// <summary>True after we've shown first-run tray-visibility guidance (balloon/dialog).</summary>
    public bool TrayGuidanceShown { get; set; }

    public static MenuConfig CreateDefault() => new()
    {
        Folders =
        [
            new MenuFolder
            {
                Name = "Tools",
                Items = []
            }
        ],
        RootItems = []
    };
}
