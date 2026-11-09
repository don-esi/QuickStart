namespace QuickStart.Models;

public sealed class MenuConfig
{
    public List<MenuFolder> Folders { get; set; } = [];
    public List<MenuItemEntry> RootItems { get; set; } = [];
    public bool StartWithWindows { get; set; }

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
