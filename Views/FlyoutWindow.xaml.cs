using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using QuickStartMenu.Models;
using QuickStartMenu.Services;

namespace QuickStartMenu.Views;

public partial class FlyoutWindow : Window
{
    private readonly MenuController _controller;
    private bool _ignoreDeactivate;
    private DispatcherTimer? _armTimer;

    public FlyoutWindow(MenuController controller)
    {
        _controller = controller;
        InitializeComponent();

        ShowActivated = true;
        Visibility = Visibility.Hidden;

        Reload(includeIcons: false);
        _controller.Changed += (_, _) =>
        {
            AppLog.Info("Config changed → flyout Reload");
            Dispatcher.BeginInvoke(() => Reload(includeIcons: true), DispatcherPriority.Background);
        };

        IsVisibleChanged += (_, _) =>
            AppLog.Info($"Flyout IsVisibleChanged → {IsVisible} Left={Left:0} Top={Top:0} Size={ActualWidth:0}x{ActualHeight:0}");

        Activated += (_, _) => AppLog.Info("Flyout Activated");
        Deactivated += (_, _) => AppLog.Info($"Flyout Deactivated (ignore={_ignoreDeactivate})");
        ContentRendered += (_, _) => AppLog.Info($"Flyout ContentRendered size={ActualWidth:0}x{ActualHeight:0}");

        AppLog.Info("FlyoutWindow ctor done");
    }

    public void Reload(bool includeIcons = true)
    {
        AppLog.Info($"Flyout Reload start includeIcons={includeIcons} folders={_controller.Config.Folders.Count} rootItems={_controller.Config.RootItems.Count}");
        MenuHost.Children.Clear();
        var config = _controller.Config;

        foreach (var folder in config.Folders)
            MenuHost.Children.Add(BuildFolderBlock(folder, includeIcons));

        if (config.RootItems.Count > 0)
        {
            if (config.Folders.Count > 0)
                MenuHost.Children.Add(CreateSectionLabel("Pinned"));

            foreach (var item in config.RootItems)
                MenuHost.Children.Add(BuildItemButton(item, includeIcons));
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

        AppLog.Info("Flyout Reload end");
    }

    /// <summary>
    /// Show near the tray click. Never call Window.Measure(∞) — that FailFasts in GetWindowMinMax.
    /// </summary>
    public void ShowNearTray()
    {
        AppLog.Info("ShowNearTray begin");

        var point = System.Windows.Forms.Control.MousePosition;
        var work = System.Windows.Forms.Screen.FromPoint(point).WorkingArea;

        // Measure CONTENT only (safe). Measuring the Window with ∞ triggers FailFast.
        var (width, height) = EstimateContentSize();
        AppLog.Info($"ShowNearTray estimated size={width:0}x{height:0}");

        var left = point.X - width / 2;
        var top = point.Y - height - 16;

        if (top < work.Top + 4)
            top = point.Y + 16;
        if (left < work.Left + 4)
            left = work.Left + 8;
        if (left + width > work.Right - 4)
            left = work.Right - width - 8;
        if (top + height > work.Bottom - 4)
            top = Math.Max(work.Top + 8, work.Bottom - height - 8);

        Left = left;
        Top = top;

        AppLog.Info(
            $"ShowNearTray mouse=({point.X},{point.Y}) work=[{work.Left},{work.Top}-{work.Right},{work.Bottom}] " +
            $"place=({left:0},{top:0})");

        ArmDeactivateGuard(TimeSpan.FromMilliseconds(500));

        try
        {
            if (!IsVisible)
            {
                Show();
                AppLog.Info("Flyout Show() called");
            }
            else
            {
                Visibility = Visibility.Visible;
                AppLog.Info("Flyout set Visible on existing HWND");
            }

            // Re-clamp after SizeToContent lays out for real.
            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    ClampToWorkingArea(point, work);
                    Activate();
                    Focus();
                    AppLog.Info($"Post-layout clamp IsActive={IsActive} IsVisible={IsVisible} bounds=({Left:0},{Top:0}) {ActualWidth:0}x{ActualHeight:0}");
                }
                catch (Exception ex)
                {
                    AppLog.Error("Post-layout clamp failed", ex);
                }
            }, DispatcherPriority.Loaded);

            // Icons after visible so shell extract never blocks open.
            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    AppLog.Info("Post-show icon enrich Reload");
                    Reload(includeIcons: true);
                }
                catch (Exception ex)
                {
                    AppLog.Error("Icon enrich failed", ex);
                }
            }, DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            AppLog.Error("ShowNearTray failed", ex);
            throw;
        }
    }

    private (double Width, double Height) EstimateContentSize()
    {
        try
        {
            if (Content is FrameworkElement root)
            {
                // Measure the content element only — never Window.Measure(∞)
                // (that FailFasts in GetWindowMinMax → "Unknown Hard Error").
                root.Measure(new Size(360, double.PositiveInfinity));
                var w = Math.Clamp(root.DesiredSize.Width, 260, 360);
                var h = Math.Clamp(root.DesiredSize.Height, 80, 520);
                return (w, h);
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Content measure failed: {ex.Message}");
        }

        return (280, 320);
    }

    private void ClampToWorkingArea(System.Drawing.Point point, System.Drawing.Rectangle work)
    {
        var width = ActualWidth > 0 ? ActualWidth : 280;
        var height = ActualHeight > 0 ? ActualHeight : 200;

        var left = Left;
        var top = Top;

        if (left < work.Left + 4)
            left = work.Left + 8;
        if (left + width > work.Right - 4)
            left = work.Right - width - 8;
        if (top < work.Top + 4)
            top = point.Y + 16;
        if (top + height > work.Bottom - 4)
            top = Math.Max(work.Top + 8, work.Bottom - height - 8);

        Left = left;
        Top = top;
    }

    public void HideFlyout(string reason)
    {
        AppLog.Info($"HideFlyout reason={reason}");
        _ignoreDeactivate = false;
        _armTimer?.Stop();
        Hide();
    }

    private void ArmDeactivateGuard(TimeSpan duration)
    {
        _ignoreDeactivate = true;
        _armTimer?.Stop();
        _armTimer = new DispatcherTimer { Interval = duration };
        _armTimer.Tick += (_, _) =>
        {
            _armTimer.Stop();
            _ignoreDeactivate = false;
            AppLog.Info("Deactivate guard cleared — clicks outside will dismiss");
        };
        _armTimer.Start();
        AppLog.Info($"Deactivate guard armed for {duration.TotalMilliseconds:0}ms");
    }

    private UIElement BuildFolderBlock(MenuFolder folder, bool includeIcons)
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
                panel.Children.Add(BuildItemButton(item, includeIcons));
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

    private Button BuildItemButton(MenuItemEntry item, bool includeIcons)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        if (includeIcons)
        {
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

        AppLog.Info($"Launch click name={item.Name} path={item.Path}");
        try
        {
            LaunchService.Launch(item.Path, item.Arguments, item.WorkingDirectory);
            AppLog.Info("Launch started OK");
            HideFlyout("launch");
        }
        catch (Exception ex)
        {
            AppLog.Error("Launch failed", ex);
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
        AppLog.Info("Flyout Manage button");
        HideFlyout("manage");
        if (System.Windows.Application.Current is App app)
            app.ShowManage();
    }

    private void OnDeactivated(object sender, EventArgs e)
    {
        if (_ignoreDeactivate)
        {
            AppLog.Info("OnDeactivated ignored (guard)");
            return;
        }

        AppLog.Info("OnDeactivated → hide");
        HideFlyout("deactivated");
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            AppLog.Info("Escape → hide");
            HideFlyout("escape");
        }
    }
}
