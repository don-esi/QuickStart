namespace QuickStartMenu.Models;

public sealed class MenuFolder
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New folder";
    public List<MenuItemEntry> Items { get; set; } = [];

    public MenuFolder Clone() => new()
    {
        Id = Id,
        Name = Name,
        Items = Items.Select(i => i.Clone()).ToList()
    };
}
