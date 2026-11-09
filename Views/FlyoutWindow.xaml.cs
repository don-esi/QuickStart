using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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
    /// Show near the tray click. Converts WinForms physical pixels → WPF DIPs
    /// (without that, high-DPI trays place the window off-screen while IsVisible=true).
    /// Never call Window.Measure(∞) — FailFasts in GetWindowMinMax.
    /// </summary>
    public void ShowNearTray()
    {
        AppLog.Info("ShowNearTray begin");

        var physicalMouse = System.Windows.Forms.Control.MousePosition;
        var physicalWork = System.Windows.Forms.Screen.FromPoint(physicalMouse).WorkingArea;

        // Create HWND early so TransformFromDevice is real.
        _ = new WindowInteropHelper(this).EnsureHandle();
        var mouse = DpiHelper.PhysicalToDip(this, physicalMouse.X, physicalMouse.Y);
        var work = DpiHelper.PhysicalToDip(this, physicalWork);
        var scale = GetDpiScale();

        var (width, height) = EstimateContentSize();
        AppLog.Info(
            $"ShowNearTray dpiScale={scale.X:0.###}x{scale.Y:0.###} " +
            $"physMouse=({physicalMouse.X},{physicalMouse.Y}) dipMouse=({mouse.X:0},{mouse.Y:0}) " +
            $"physWork=[{physicalWork.Left},{physicalWork.Top}-{physicalWork.Right},{physicalWork.Bottom}] " +
            $"dipWork=[{work.Left:0},{work.Top:0}-{work.Right:0},{work.Bottom:0}] est={width:0}x{height:0}");

        var left = mouse.X - width / 2;
        var top = mouse.Y - height - 16;

        if (top < work.Top + 4)
            top = mouse.Y + 16;
        if (left < work.Left + 4)
            left = work.Left + 8;
        if (left + width > work.Right - 4)
            left = work.Right - width - 8;
        if (top + height > work.Bottom - 4)
            top = Math.Max(work.Top + 8, work.Bottom - height - 8);

        Left = left;
        Top = top;
        AppLog.Info($"ShowNearTray placeDIP=({left:0},{top:0}) sizeDIP={width:0}x{height:0}");

        ArmDeactivateGuard(TimeSpan.FromMilliseconds(500));

        try
        {
            Topmost = true;
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

            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    ClampToDipWorkArea(mouse, work);
                    Activate();
                    Focus();
                    AppLog.Info(
                        $"Post-layout IsActive={IsActive} IsVisible={IsVisible} " +
                        $"boundsDIP=({Left:0},{Top:0}) {ActualWidth:0}x{ActualHeight:0} " +
                        $"onScreen={IsOnScreen(work)}");
                }
                catch (Exception ex)
                {
                    AppLog.Error("Post-layout clamp failed", ex);
                }
            }, DispatcherPriority.Loaded);

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

    private Point GetDpiScale()
    {
        var m = DpiHelper.GetTransformFromDevice(this);
        // TransformFromDevice scales physical→DIP; scale factors are M11/M22.
        var sx = m.M11 == 0 ? 1 : 1 / m.M11;
        var sy = m.M22 == 0 ? 1 : 1 / m.M22;
        return new Point(sx, sy);
    }

    private bool IsOnScreen(Rect workDip)
    {
        var r = new Rect(Left, Top, Math.Max(ActualWidth, 1), Math.Max(ActualHeight, 1));
        return r.IntersectsWith(workDip);
    }

    private (double Width, double Height) EstimateContentSize()
    {
        try
        {
            if (Content is FrameworkElement root)
            {
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

    private void ClampToDipWorkArea(Point mouseDip, Rect workDip)
    {
        var width = ActualWidth > 0 ? ActualWidth : 280;
        var height = ActualHeight > 0 ? ActualHeight : 200;

        var left = Left;
        var top = Top;

        if (left < workDip.Left + 4)
            left = workDip.Left + 8;
        if (left + width > workDip.Right - 4)
            left = workDip.Right - width - 8;
        if (top < workDip.Top + 4)
            top = mouseDip.Y + 16;
        if (top + height > workDip.Bottom - 4)
            top = Math.Max(workDip.Top + 8, workDip.Bottom - height - 8);

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
