using System.Windows;
using System.Windows.Threading;
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
    private HashSet<string>? _trayBaseline;
    private DispatcherTimer? _promoteTimer;
    private int _promoteAttempts;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Note: Environment.FailFast (e.g. WPF Window.Measure ∞) bypasses these handlers.
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Error("DispatcherUnhandledException", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Error(
                $"UnhandledException IsTerminating={args.IsTerminating}",
                args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("UnobservedTaskException", args.Exception);
            args.SetObserved();
        };

        AppLog.Info($"Startup begin. build=2026-11-09-tray-promote-quiet Log={AppLog.LogFilePath}");
        AppLog.Info($"ProcessPath={Environment.ProcessPath}; BaseDir={AppContext.BaseDirectory}");
        AppLog.Info($"Args=[{string.Join(' ', e.Args)}]");
        AppLog.Info($"OS build={Environment.OSVersion.Version.Build} Win11Promote={TrayIconPromoter.IsWindows11OrLater}");

        try
        {
            var store = new ConfigStore();
            AppLog.Info($"Config path={store.ConfigPath}");
            _controller = new MenuController(store);
            AppLog.Info(
                $"Config loaded: folders={_controller.Config.Folders.Count}, " +
                $"rootItems={_controller.Config.RootItems.Count}, " +
                $"startWithWindows={_controller.Config.StartWithWindows}");

            var swept = TrayIconPromoter.SweepStaleEntries();
            if (swept > 0)
                AppLog.Info($"TrayIconPromoter swept {swept} stale NotifyIconSettings entr(y/ies)");

            _trayBaseline = TrayIconPromoter.CaptureBaseline();
            AppLog.Info($"Tray baseline subkeys={_trayBaseline?.Count.ToString() ?? "n/a"}");

            _trayIcon = IconService.CreateTrayIcon();
            _tray = new Forms.NotifyIcon
            {
                Icon = _trayIcon,
                Text = "Quick Start Menu — left-click to open",
                Visible = true
            };

            // MouseClick (not MouseUp) for a clean single left-click; marshal to WPF dispatcher.
            _tray.MouseClick += TrayOnMouseClick;
            _tray.ContextMenuStrip = BuildTrayMenu();
            AppLog.Info("Tray icon created (MouseClick + ContextMenuStrip).");

            // Silent best-effort promote only — no first-run balloon/dialog (Windows prompts itself).
            ScheduleTrayPromotion();

            _flyout = new FlyoutWindow(_controller);
            AppLog.Info("FlyoutWindow constructed (hidden).");

            if (e.Args.Any(a => string.Equals(a, "--smoke-flyout", StringComparison.OrdinalIgnoreCase)))
                ScheduleSmokeFlyout();
        }
        catch (Exception ex)
        {
            AppLog.Error("Startup failed", ex);
            Forms.MessageBox.Show(
                $"Quick Start Menu failed to start.\n\n{ex.Message}\n\nSee log:\n{AppLog.LogFilePath}",
                "Quick Start Menu",
                Forms.MessageBoxButtons.OK,
                Forms.MessageBoxIcon.Error);
            Shutdown(-1);
        }
    }

    private void ScheduleTrayPromotion()
    {
        if (!TrayIconPromoter.IsWindows11OrLater)
        {
            AppLog.Info("Tray promote skipped (not Windows 11+ / no NotifyIconSettings schema)");
            return;
        }

        _promoteAttempts = 0;
        _promoteTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
        _promoteTimer.Tick += (_, _) =>
        {
            _promoteAttempts++;
            var done = TrayIconPromoter.TryPromote(_trayBaseline);
            AppLog.Info($"Tray promote attempt {_promoteAttempts}: identified={done}");
            if (done || _promoteAttempts >= 12)
            {
                _promoteTimer.Stop();
                _promoteTimer = null;
            }
        };
        _promoteTimer.Start();
    }

    private void ScheduleSmokeFlyout()
    {
        AppLog.Info("Smoke: scheduling flyout open");
        var open = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        open.Tick += (_, _) =>
        {
            open.Stop();

            // Simulate a tray-corner click (high physical coords) — the DPI bug only
            // showed up there; center-screen smoke can pass while tray clicks fail.
            var screen = Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0];
            var trayish = new System.Drawing.Point(
                screen.WorkingArea.Right - 40,
                screen.WorkingArea.Bottom - 8);
            Forms.Cursor.Position = trayish;
            AppLog.Info($"Smoke: cursor forced to trayish phys=({trayish.X},{trayish.Y})");

            ShowFlyout();
            AppLog.Info($"Smoke: after ShowFlyout IsVisible={_flyout?.IsVisible} IsActive={_flyout?.IsActive} Left={_flyout?.Left:0} Top={_flyout?.Top:0}");

            var check = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            check.Tick += (_, _) =>
            {
                check.Stop();
                var visible = _flyout is { IsVisible: true };
                var onScreen = false;
                if (_flyout is not null)
                {
                    var work = DpiHelper.PhysicalToDip(_flyout, screen.WorkingArea);
                    var bounds = new Rect(_flyout.Left, _flyout.Top,
                        Math.Max(_flyout.ActualWidth, 1), Math.Max(_flyout.ActualHeight, 1));
                    onScreen = bounds.IntersectsWith(work);
                    AppLog.Info($"Smoke: boundsDIP={bounds} workDIP={work} onScreen={onScreen}");
                }

                var ok = visible && onScreen;
                AppLog.Info($"Smoke: result={(ok ? "PASS" : "FAIL")} visible={visible} onScreen={onScreen}");
                Shutdown(ok ? 0 : 2);
            };
            check.Start();
        };
        open.Start();
    }

    private Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open menu", null, (_, _) =>
        {
            AppLog.Info("Tray context: Open menu");
            Dispatcher.BeginInvoke(ShowFlyout, DispatcherPriority.Normal);
        });
        menu.Items.Add("Manage…", null, (_, _) =>
        {
            AppLog.Info("Tray context: Manage");
            Dispatcher.BeginInvoke(ShowManage, DispatcherPriority.Normal);
        });
        menu.Items.Add("Keep icon visible…", null, (_, _) =>
        {
            AppLog.Info("Tray context: Keep icon visible");
            Dispatcher.BeginInvoke(() =>
            {
                TrayIconPromoter.TryPromote(_trayBaseline);
                TrayVisibilityGuidance.OpenTaskbarSettings();
            }, DispatcherPriority.Normal);
        });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) =>
        {
            AppLog.Info("Tray context: Exit");
            Dispatcher.BeginInvoke(Shutdown, DispatcherPriority.Normal);
        });
        return menu;
    }

    private void TrayOnMouseClick(object? sender, Forms.MouseEventArgs e)
    {
        AppLog.Info($"Tray MouseClick button={e.Button} at WinForms({e.X},{e.Y}) screen={Forms.Control.MousePosition}");

        if (e.Button != Forms.MouseButtons.Left)
            return;

        // Defer past the shell/tray click so the new window isn't immediately deactivated.
        Dispatcher.BeginInvoke(() =>
        {
            AppLog.Info("Dispatcher: handling left-click → ShowFlyout");
            ShowFlyout();
        }, DispatcherPriority.ApplicationIdle);
    }

    private void ShowFlyout()
    {
        if (_controller is null)
        {
            AppLog.Warn("ShowFlyout aborted: controller null");
            return;
        }

        try
        {
            _flyout ??= new FlyoutWindow(_controller);

            if (_flyout.IsVisible)
            {
                AppLog.Info("Flyout already visible — toggling closed.");
                _flyout.HideFlyout("toggle");
                return;
            }

            AppLog.Info("ShowFlyout: fast reload (no icons) + show");
            _flyout.Reload(includeIcons: false);
            _flyout.ShowNearTray();
        }
        catch (Exception ex)
        {
            AppLog.Error("ShowFlyout failed", ex);
        }
    }

    internal void ShowManage()
    {
        if (_controller is null)
            return;

        AppLog.Info("ShowManage");
        if (_manage is { IsLoaded: true })
        {
            _manage.Activate();
            return;
        }

        _manage = new ManageWindow(_controller);
        _manage.Closed += (_, _) =>
        {
            AppLog.Info("ManageWindow closed");
            _manage = null;
        };
        _manage.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLog.Info($"OnExit code={e.ApplicationExitCode}");
        _promoteTimer?.Stop();
        _promoteTimer = null;

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
