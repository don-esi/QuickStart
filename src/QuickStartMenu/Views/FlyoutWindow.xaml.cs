using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QuickStartMenu.Models;
using QuickStartMenu.Services;

namespace QuickStartMenu.Views;

public partial class FlyoutWindow : Window
{
    private readonly MenuController _controller;

    public FlyoutWindow(MenuController controller)
    {
        _controller = controller;
        InitializeComponent();
        Reload();
        _controller.Changed += (_, _) => Dispatcher.Invoke(Reload);
    }

    public void Reload()
    {
        MenuHost.Children.Clear();
        var config = _controller.Config;

        foreach (var folder in config.Folders)
            MenuHost.Children.Add(BuildFolderBlock(folder));

        if (config.RootItems.Count > 0)
        {
            if (config.Folders.Count > 0)
                MenuHost.Children.Add(CreateSectionLabel("Pinned"));

            foreach (var item in config.RootItems)
                MenuHost.Children.Add(BuildItemButton(item));
        }

        if (config.Folders.Count == 0 && config.RootItems.Count == 0)
        {
            MenuHost.Children.Add(new TextBlock
            {
                Text = "No items yet — open Manage to add some.",
                Foreground = (Brush)FindResource("MutedBrush"),
                FontFamily = (FontFamily)FindResource("UiFont"),
                FontSize = 12,
                Margin = new Thickness(8, 12, 8, 12),
                TextWrapping = TextWrapping.Wrap
            });
        }
    }

    public void ShowNearCursor()
    {
        var point = System.Windows.Forms.Control.MousePosition;
        var work = System.Windows.Forms.Screen.FromPoint(point).WorkingArea;

        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Arrange(new Rect(DesiredSize));

        var width = ActualWidth > 0 ? ActualWidth : DesiredSize.Width;
        var height = ActualHeight > 0 ? ActualHeight : DesiredSize.Height;

        var left = point.X - width / 2;
        var top = point.Y - height - 12;

        if (top < work.Top)
            top = point.Y + 12;
        if (left < work.Left)
            left = work.Left + 8;
        if (left + width > work.Right)
            left = work.Right - width - 8;
        if (top + height > work.Bottom)
            top = work.Bottom - height - 8;

        Left = left;
        Top = top;
        Show();
        Activate();
    }

    private UIElement BuildFolderBlock(MenuFolder folder)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
        panel.Children.Add(CreateSectionLabel(folder.Name));

        if (folder.Items.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "(empty)",
                Foreground = (Brush)FindResource("MutedBrush"),
                FontSize = 11,
                Margin = new Thickness(10, 0, 0, 4)
            });
        }
        else
        {
            foreach (var item in folder.Items)
                panel.Children.Add(BuildItemButton(item));
        }

        return panel;
    }

    private TextBlock CreateSectionLabel(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        FontFamily = (FontFamily)FindResource("UiFont"),
        FontSize = 10,
        FontWeight = FontWeights.SemiBold,
        Foreground = (Brush)FindResource("MutedBrush"),
        Margin = new Thickness(8, 6, 8, 4)
    };

    private Button BuildItemButton(MenuItemEntry item)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = IconService.GetIcon(item.Path);
        if (icon is not null)
        {
            content.Children.Add(new Image
            {
                Source = icon,
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        content.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(item.Name) ? item.Path : item.Name,
            FontFamily = (FontFamily)FindResource("UiFont"),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        var button = new Button
        {
            Content = content,
            Style = (Style)FindResource("FlatButton"),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Tag = item,
            ToolTip = item.Path
        };
        button.Click += Item_Click;
        return button;
    }

    private void Item_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: MenuItemEntry item })
            return;

        try
        {
            LaunchService.Launch(item.Path, item.Arguments, item.WorkingDirectory);
            Hide();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Could not launch:\n{item.Path}\n\n{ex.Message}",
                "Quick Start Menu",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void Manage_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        if (System.Windows.Application.Current is App app)
            app.ShowManage();
    }

    private void OnDeactivated(object sender, EventArgs e) => Hide();

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Hide();
    }
}
