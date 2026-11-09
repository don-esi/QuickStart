using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QuickStartMenu.Models;
using QuickStartMenu.Services;

namespace QuickStartMenu.Views;

public partial class ManageWindow : Window
{
    private readonly MenuController _controller;
    private Point _dragStart;
    private TreeViewItem? _dragItem;
    private bool _suppressDetailWrite;

    public ManageWindow(MenuController controller)
    {
        _controller = controller;
        InitializeComponent();
        StartupCheck.IsChecked = _controller.Config.StartWithWindows;
        StatusText.Text = _controller.ConfigPath;
        RebuildTree();
    }

    private void RebuildTree()
    {
        MenuTree.Items.Clear();
        var config = _controller.Config;

        foreach (var folder in config.Folders)
        {
            var folderNode = CreateFolderNode(folder);
            foreach (var item in folder.Items)
                folderNode.Items.Add(CreateItemNode(item, folder.Id));
            folderNode.IsExpanded = true;
            MenuTree.Items.Add(folderNode);
        }

        var root = new TreeViewItem
        {
            Header = BuildHeader("Root items", isFolder: true),
            Tag = new NodeTag(NodeKind.Root, null, null),
            IsExpanded = true
        };
        foreach (var item in config.RootItems)
            root.Items.Add(CreateItemNode(item, folderId: null));
        MenuTree.Items.Add(root);
    }

    private TreeViewItem CreateFolderNode(MenuFolder folder) => new()
    {
        Header = BuildHeader(folder.Name, isFolder: true),
        Tag = new NodeTag(NodeKind.Folder, folder.Id, null),
        AllowDrop = true
    };

    private TreeViewItem CreateItemNode(MenuItemEntry item, string? folderId)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = IconService.GetIcon(item.Path);
        if (icon is not null)
        {
            header.Children.Add(new Image
            {
                Source = icon,
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 6, 0)
            });
        }

        header.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(item.Name) ? item.Path : item.Name,
            VerticalAlignment = VerticalAlignment.Center
        });

        return new TreeViewItem
        {
            Header = header,
            Tag = new NodeTag(NodeKind.Item, folderId, item.Id)
        };
    }

    private static object BuildHeader(string text, bool isFolder) => new TextBlock
    {
        Text = isFolder ? $"[ ]  {text}" : text,
        FontWeight = isFolder ? FontWeights.SemiBold : FontWeights.Normal
    };

    private void MenuTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        _suppressDetailWrite = true;
        try
        {
            if (MenuTree.SelectedItem is not TreeViewItem { Tag: NodeTag tag })
            {
                ClearDetails();
                return;
            }

            if (tag.Kind == NodeKind.Item)
            {
                var item = FindItem(tag.ItemId!);
                if (item is null)
                {
                    ClearDetails();
                    return;
                }

                NameBox.IsEnabled = true;
                PathBox.IsEnabled = true;
                ArgsBox.IsEnabled = true;
                WorkDirBox.IsEnabled = true;
                NameBox.Text = item.Name;
                PathBox.Text = item.Path;
                ArgsBox.Text = item.Arguments;
                WorkDirBox.Text = item.WorkingDirectory;
            }
            else if (tag.Kind == NodeKind.Folder)
            {
                var folder = _controller.Config.Folders.FirstOrDefault(f => f.Id == tag.FolderId);
                NameBox.IsEnabled = true;
                PathBox.IsEnabled = false;
                ArgsBox.IsEnabled = false;
                WorkDirBox.IsEnabled = false;
                NameBox.Text = folder?.Name ?? "";
                PathBox.Text = "";
                ArgsBox.Text = "";
                WorkDirBox.Text = "";
            }
            else
            {
                ClearDetails();
            }
        }
        finally
        {
            _suppressDetailWrite = false;
        }
    }

    private void ClearDetails()
    {
        NameBox.IsEnabled = PathBox.IsEnabled = ArgsBox.IsEnabled = WorkDirBox.IsEnabled = false;
        NameBox.Text = PathBox.Text = ArgsBox.Text = WorkDirBox.Text = "";
    }

    private void Detail_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressDetailWrite)
            return;
        if (MenuTree.SelectedItem is not TreeViewItem { Tag: NodeTag tag })
            return;

        if (tag.Kind == NodeKind.Item)
        {
            var item = FindItem(tag.ItemId!);
            if (item is null)
                return;
            item.Name = NameBox.Text.Trim();
            item.Path = PathBox.Text.Trim();
            item.Arguments = ArgsBox.Text.Trim();
            item.WorkingDirectory = WorkDirBox.Text.Trim();
        }
        else if (tag.Kind == NodeKind.Folder)
        {
            var folder = _controller.Config.Folders.FirstOrDefault(f => f.Id == tag.FolderId);
            if (folder is null)
                return;
            folder.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? "Folder" : NameBox.Text.Trim();
        }

        _controller.Save();
        var selectedId = tag.ItemId ?? tag.FolderId;
        RebuildTree();
        Reselect(selectedId);
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        _controller.AddFolder("New folder");
        RebuildTree();
        StatusText.Text = "Folder added.";
    }

    private void AddFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add executable, script, or shortcut",
            Filter = "Launchables|*.exe;*.cmd;*.bat;*.ps1;*.lnk;*.msc;*.cpl|All files|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog() != true)
            return;

        var folderId = GetDropTargetFolderId(MenuTree.SelectedItem as TreeViewItem);
        foreach (var file in dialog.FileNames)
            AddPath(file, folderId);

        RebuildTree();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (MenuTree.SelectedItem is not TreeViewItem { Tag: NodeTag tag })
            return;

        if (tag.Kind == NodeKind.Item)
        {
            RemoveItem(tag.ItemId!);
        }
        else if (tag.Kind == NodeKind.Folder)
        {
            var folder = _controller.Config.Folders.FirstOrDefault(f => f.Id == tag.FolderId);
            if (folder is null)
                return;
            if (folder.Items.Count > 0)
            {
                var result = MessageBox.Show(
                    this,
                    $"Delete folder \"{folder.Name}\" and its {folder.Items.Count} item(s)?",
                    "Confirm",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                    return;
            }

            _controller.Config.Folders.RemoveAll(f => f.Id == tag.FolderId);
            _controller.Save();
        }

        RebuildTree();
        ClearDetails();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Detail_Changed(sender, e);
        _controller.Save();
        StatusText.Text = $"Saved · {_controller.ConfigPath}";
    }

    private void StartupCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;
        _controller.SetStartWithWindows(StartupCheck.IsChecked == true);
        StatusText.Text = StartupCheck.IsChecked == true
            ? "Will start with Windows."
            : "Removed from Windows startup.";
    }

    private void ImportPinned_Click(object sender, RoutedEventArgs e)
    {
        var folderId = GetDropTargetFolderId(MenuTree.SelectedItem as TreeViewItem);
        var added = _controller.ImportPinnedApps(folderId);
        RebuildTree();
        StatusText.Text = added == 0
            ? "No new taskbar pins found (or all already imported)."
            : $"Imported {added} pinned app(s).";
    }

    private void TraySettings_Click(object sender, RoutedEventArgs e)
    {
        TrayIconPromoter.TryPromote(baseline: null);
        TrayVisibilityGuidance.OpenTaskbarSettings();
        StatusText.Text = "Opened Taskbar settings — enable Quick Start Menu under Other system tray icons.";
        MessageBox.Show(
            this,
            TrayVisibilityGuidance.ManualSteps,
            "Tray icon visibility",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void Tree_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(typeof(NodeTag)))
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }
    }

    private void Tree_Drop(object sender, System.Windows.DragEventArgs e)
    {
        var target = FindAncestor<TreeViewItem>((DependencyObject?)e.OriginalSource) ?? MenuTree.SelectedItem as TreeViewItem;
        var folderId = GetDropTargetFolderId(target);

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                foreach (var file in files)
                    AddPath(file, folderId);
                RebuildTree();
                StatusText.Text = $"Added {files.Length} item(s).";
            }
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(typeof(NodeTag)) && e.Data.GetData(typeof(NodeTag)) is NodeTag sourceTag
            && sourceTag.Kind == NodeKind.Item)
        {
            MoveItem(sourceTag.ItemId!, folderId, target);
            RebuildTree();
            Reselect(sourceTag.ItemId);
            e.Handled = true;
        }
    }

    private void MenuTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(MenuTree);
        _dragItem = FindAncestor<TreeViewItem>((DependencyObject?)e.OriginalSource);
    }

    private void MenuTree_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragItem?.Tag is not NodeTag tag || tag.Kind != NodeKind.Item)
            return;

        var pos = e.GetPosition(MenuTree);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        DragDrop.DoDragDrop(_dragItem, tag, DragDropEffects.Move);
    }

    private void MenuTree_GiveFeedback(object sender, GiveFeedbackEventArgs e)
    {
        e.UseDefaultCursors = true;
        e.Handled = true;
    }

    private void AddPath(string path, string? folderId)
    {
        path = path.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(path))
            return;

        string name;
        string target = path;
        string args = "";
        string workDir = "";

        if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            var resolved = ShortcutResolver.Resolve(path);
            name = Path.GetFileNameWithoutExtension(path);
            if (resolved is not null)
            {
                target = resolved.TargetPath;
                args = resolved.Arguments ?? "";
                workDir = resolved.WorkingDirectory ?? "";
            }
        }
        else
        {
            name = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(name))
                name = Path.GetFileName(path);
        }

        _controller.AddItem(new MenuItemEntry
        {
            Name = name,
            Path = target,
            Arguments = args,
            WorkingDirectory = workDir
        }, folderId);
    }

    private void MoveItem(string itemId, string? targetFolderId, TreeViewItem? targetNode)
    {
        var item = FindItem(itemId);
        if (item is null)
            return;

        _controller.Config.RootItems.RemoveAll(i => i.Id == itemId);
        foreach (var folder in _controller.Config.Folders)
            folder.Items.RemoveAll(i => i.Id == itemId);

        if (targetNode?.Tag is NodeTag { Kind: NodeKind.Item } targetTag)
        {
            var list = targetTag.FolderId is null
                ? _controller.Config.RootItems
                : _controller.Config.Folders.First(f => f.Id == targetTag.FolderId).Items;
            var index = list.FindIndex(i => i.Id == targetTag.ItemId);
            if (index < 0)
                list.Add(item);
            else
                list.Insert(index, item);
        }
        else if (targetFolderId is null)
        {
            _controller.Config.RootItems.Add(item);
        }
        else
        {
            var folder = _controller.Config.Folders.FirstOrDefault(f => f.Id == targetFolderId);
            folder?.Items.Add(item);
        }

        _controller.Save();
    }

    private void RemoveItem(string itemId)
    {
        _controller.Config.RootItems.RemoveAll(i => i.Id == itemId);
        foreach (var folder in _controller.Config.Folders)
            folder.Items.RemoveAll(i => i.Id == itemId);
        _controller.Save();
    }

    private MenuItemEntry? FindItem(string itemId) =>
        _controller.Config.RootItems.FirstOrDefault(i => i.Id == itemId)
        ?? _controller.Config.Folders.SelectMany(f => f.Items).FirstOrDefault(i => i.Id == itemId);

    private string? GetDropTargetFolderId(TreeViewItem? node)
    {
        while (node is not null)
        {
            if (node.Tag is NodeTag tag)
            {
                if (tag.Kind == NodeKind.Folder)
                    return tag.FolderId;
                if (tag.Kind == NodeKind.Root)
                    return null;
                if (tag.Kind == NodeKind.Item)
                    return tag.FolderId;
            }

            node = ItemsControl.ItemsControlFromItemContainer(node) as TreeViewItem;
        }

        return null;
    }

    private void Reselect(string? id)
    {
        if (id is null)
            return;

        foreach (TreeViewItem top in MenuTree.Items)
        {
            if (TrySelect(top, id))
                return;
        }
    }

    private static bool TrySelect(TreeViewItem node, string id)
    {
        if (node.Tag is NodeTag tag && (tag.ItemId == id || tag.FolderId == id && tag.Kind == NodeKind.Folder))
        {
            node.IsSelected = true;
            node.BringIntoView();
            return true;
        }

        foreach (TreeViewItem child in node.Items)
        {
            if (TrySelect(child, id))
                return true;
        }

        return false;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private enum NodeKind { Root, Folder, Item }

    private sealed record NodeTag(NodeKind Kind, string? FolderId, string? ItemId);
}
