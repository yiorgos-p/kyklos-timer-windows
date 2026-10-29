using Kyklos.Engine;
using Kyklos.Platform;
using Kyklos.Views;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using Windows.Win32;

namespace Kyklos;

/// <summary>
/// Starts kyklos and owns what lives as long as it does: the settings, the timer, the tray icon, and which of its
/// surfaces is showing. There's one timer on screen at a time: the window, or the tray flyout.
/// </summary>
public partial class App : Application
{
    /// <summary>Taskbar grouping and notifications go by this ID, as they go by the bundle ID on the Mac.</summary>
    private const string AppUserModelId = "io.github.yiorgos-p.Kyklos";

    /// <summary>
    /// Clicking the tray icon while the flyout is open first takes the focus from it, which closes it; a click that
    /// comes this soon after is that same click, so it doesn't open the flyout again.
    /// </summary>
    private const long FlyoutReopenDelay = 300;

    private Settings _settings = null!;
    private TimerViewModel _timer = null!;
    private Notifier _notifier = null!;
    private SystemWindow _system = null!;
    private TrayIcon _tray = null!;
    private MainWindow? _mainWindow;
    private TrayFlyout? _flyout;
    private long _flyoutClosedAt;
    private bool _trayPending;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        PInvoke.SetCurrentProcessExplicitAppUserModelID(AppUserModelId);

        // kyklos keeps running in the tray when its windows close; only Quit ends it.
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;

        var queue = DispatcherQueue.GetForCurrentThread();
        _settings = new Settings(Settings.DefaultFolder);
        _notifier = new Notifier(() => queue.TryEnqueue(ShowMainWindow));
        _timer = new TimerViewModel(_settings, new SystemClock(), new DispatcherScheduler(queue), new AlarmPlayer(queue), _notifier);

        _system = new SystemWindow();
        _tray = new TrayIcon(_system);
        _tray.Selected += ToggleFlyout;
        _tray.MenuRequested += ShowTrayMenu;
        _timer.PropertyChanged += (_, _) => ScheduleTrayUpdate(queue);
        UpdateTray();

        ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        _flyout?.Close();

        if (_mainWindow is null)
        {
            var window = new MainWindow(_timer, _settings);
            window.Closed += (_, _) =>
            {
                // The close button: the window is gone, and kyklos goes on in the tray.
                if (_mainWindow == window)
                {
                    _mainWindow = null;
                }
            };
            window.View.SettingsRequested += ShowSettings;
            _mainWindow = window;
        }

        _mainWindow.BringToFront();
    }

    private void ToggleFlyout(PointInt32 click)
    {
        if (_flyout is not null || Environment.TickCount64 - _flyoutClosedAt < FlyoutReopenDelay)
        {
            _flyout?.Close();
            return;
        }

        // The window gives way, as on the Mac it fades back into the menu bar.
        if (_mainWindow is { IsShowing: true } window)
        {
            _mainWindow = null;
            window.FadeOutAndClose();
        }

        var flyout = new TrayFlyout(_timer, _settings);
        flyout.Closed += (_, _) =>
        {
            _flyout = null;
            _flyoutClosedAt = Environment.TickCount64;
        };
        flyout.View.OpenWindowRequested += ShowMainWindow;
        flyout.View.SettingsRequested += ShowSettings;
        flyout.View.QuitRequested += Quit;
        _flyout = flyout;
        flyout.Open(_tray.Bounds, click);
    }

    private void ShowTrayMenu(PointInt32 point, bool byKeyboard) =>
        TrayMenu.Show(point, byKeyboard, _timer, _settings.Appearance, ShowMainWindow, ShowSettings, Quit);

    private void ShowSettings()
    {
    }

    /// <summary>Several properties change together on each tick; redraw the tray icon once, after they all have.</summary>
    private void ScheduleTrayUpdate(DispatcherQueue queue)
    {
        if (_trayPending)
        {
            return;
        }

        _trayPending = true;
        queue.TryEnqueue(() =>
        {
            _trayPending = false;
            UpdateTray();
        });
    }

    private void UpdateTray()
    {
        var (tone, fraction) = TrayIndicator.For(_timer);
        _tray.Show(tone, fraction, _timer.TrayTitle);
    }

    private void Quit()
    {
        // The last keys typed aren't lost, and a notification left on screen launches kyklos again.
        _timer.SaveTask();
        _notifier.Unregister();
        _tray.Dispose();
        _system.Dispose();
        _flyout?.Close();
        _mainWindow?.Close();
        Exit();
    }
}
