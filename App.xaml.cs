using System.Windows;
using QuickStartMenu.Services;
using QuickStartMenu.Views;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace QuickStartMenu;

public partial class App : System.Windows.Application
{
    private Forms.NotifyIcon? _tray;
    private Drawing.Icon? _trayIcon;
    private MenuController? _controller;
    private FlyoutWindow? _flyout;
    private ManageWindow? _manage;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var store = new ConfigStore();
        _controller = new MenuController(store);

        _trayIcon = IconService.CreateTrayIcon();
        _tray = new Forms.NotifyIcon
        {
            Icon = _trayIcon,
            Text = "Quick Start Menu",
            Visible = true
        };

        _tray.MouseUp += TrayOnMouseUp;
        _tray.ContextMenuStrip = BuildTrayMenu();

        // Keep a hidden reference so WPF stays alive without a main window.
        _flyout = new FlyoutWindow(_controller);
    }

    private Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open menu", null, (_, _) => ShowFlyout());
        menu.Items.Add("Manage…", null, (_, _) => ShowManage());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Shutdown());
        return menu;
    }

    private void TrayOnMouseUp(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
            ShowFlyout();
    }

    private void ShowFlyout()
    {
        if (_controller is null)
            return;

        _flyout ??= new FlyoutWindow(_controller);
        _flyout.Reload();
        _flyout.ShowNearCursor();
    }

    internal void ShowManage()
    {
        if (_controller is null)
            return;

        if (_manage is { IsLoaded: true })
        {
            _manage.Activate();
            return;
        }

        _manage = new ManageWindow(_controller);
        _manage.Closed += (_, _) => _manage = null;
        _manage.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }

        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
