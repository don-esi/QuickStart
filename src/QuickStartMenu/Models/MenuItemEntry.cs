namespace QuickStartMenu.Models;

public sealed class MenuItemEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;

    public MenuItemEntry Clone() => new()
    {
        Id = Id,
        Name = Name,
        Path = Path,
        Arguments = Arguments,
        WorkingDirectory = WorkingDirectory
    };
}
