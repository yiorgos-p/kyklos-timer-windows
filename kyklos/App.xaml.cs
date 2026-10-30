using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using Kyklos.Engine;
using Kyklos.Platform;
using Kyklos.Views;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.Graphics;
using Windows.Win32;

namespace Kyklos;

/// <summary>
/// Starts kyklos and owns what lives as long as it does: the settings, the timer, the tray icon, and which of its
/// surfaces is showing. There's one timer on screen at a time: the window, the tray flyout, or the mini timer.
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
    private GlobalHotKey _hotKey = null!;
    private TimeJumps _timeJumps = null!;
    private NewDay _newDay = null!;
    private MainWindow? _mainWindow;
    private TrayFlyout? _flyout;
    private MiniTimer? _miniTimer;
    private SettingsWindow? _settingsWindow;
    private TrayMenu? _trayMenu;
    private long _flyoutClosedAt;
    private bool _updatePending;

    /// <summary>The timer's state as last settled, which Minimal mode compares against.</summary>
    private TimerState _settledState;

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
        var clock = new SystemClock();
        var scheduler = new DispatcherScheduler(queue);
        _settings = new Settings(Settings.DefaultFolder);
        _notifier = new Notifier(() => queue.TryEnqueue(ShowMainWindow));
        _timer = new TimerViewModel(_settings, clock, scheduler, new AlarmPlayer(queue), _notifier);
        _settledState = _timer.State;
        _newDay = new NewDay(clock, scheduler, _timer.LoadTodaysSessions);

        _system = new SystemWindow();
        _tray = new TrayIcon(_system);
        _tray.Selected += ToggleFlyout;
        _tray.MenuRequested += ShowTrayMenu;

        // From any app, the same as the main button.
        _hotKey = new GlobalHotKey(_system, _settings);
        _hotKey.Pressed += _timer.ToggleTimer;

        // Timers don't fire while the PC sleeps, and a new clock moves midnight: catch up at once.
        _timeJumps = new TimeJumps(_system);
        _timeJumps.Happened += () =>
        {
            _timer.CatchUp();
            _newDay.Recheck();
        };

        _system.Received += (message, wParam, lParam) =>
        {
            // Signing out or shutting down ends kyklos without a Quit, so keep the task typed then too.
            if (message == PInvoke.WM_ENDSESSION && wParam != 0)
            {
                _timer.SaveTask();
            }

            // Windows writes times another way now (12 or 24 hours), which .NET keeps until told: "ends 14:35" follows.
            if (message == PInvoke.WM_SETTINGCHANGE && lParam != 0 && Marshal.PtrToStringUni(lParam) == "intl")
            {
                CultureInfo.CurrentCulture.ClearCachedData();
            }
        };

        _timer.PropertyChanged += (_, _) => ScheduleUpdate(queue);
        _settings.PropertyChanged += OnSettingChanged;
        UpdateTray();

        // Launched again while running, from Start or a taskbar pin: the new copy hands over, and this one shows its window.
        var instance = AppInstance.GetCurrent();
        instance.Activated += (_, _) => queue.TryEnqueue(ShowMainWindow);

        // A "time's up" notification left from before opens the window, as it does while kyklos runs.
        Show(instance.GetActivatedEventArgs().Kind == ExtendedActivationKind.AppNotification
            ? TimerSurface.MainWindow
            : MinimalModeRules.AtLaunch(_settings.MinimalMode));
    }

    private void Show(TimerSurface? surface)
    {
        switch (surface)
        {
            case TimerSurface.MainWindow:
                ShowMainWindow();
                break;
            case TimerSurface.MiniTimer:
                ShowMiniTimer();
                break;
        }
    }

    /// <summary>
    /// The window is built once and then hidden and shown, unlike the other surfaces: WinUI never frees a window with
    /// its own title bar, so one thrown away each time would leave a megabyte or two behind.
    /// </summary>
    private void ShowMainWindow()
    {
        _flyout?.Close();
        CloseMiniTimer();

        if (_mainWindow is null)
        {
            _mainWindow = new MainWindow(_timer, _settings);
            _mainWindow.View.SettingsRequested += ShowSettings;
            _mainWindow.View.MiniTimerRequested += ShowMiniTimer;
        }

        _mainWindow.BringToFront();
    }

    /// <summary>The mini timer takes the window's place, without taking the keyboard from the app being worked in.</summary>
    private void ShowMiniTimer()
    {
        _flyout?.Close();
        _mainWindow?.Hide();

        if (_miniTimer is null)
        {
            var miniTimer = new MiniTimer(_timer, _settings);

            // Its ×: it comes back when the next session starts, and the tray opens the window meanwhile. (The handler
            // finds the window through sender: WinUI keeps a window's handlers after it closes, so one holding on to the
            // window would keep it from ever being freed.)
            miniTimer.Closed += (sender, _) =>
            {
                if (ReferenceEquals(_miniTimer, sender))
                {
                    _miniTimer = null;
                }

                ClosedWindows.LetGo();
            };
            miniTimer.OpenWindowRequested += ShowMainWindow;
            _miniTimer = miniTimer;
        }

        _miniTimer.Show();
    }

    private void CloseMiniTimer()
    {
        if (_miniTimer is { } miniTimer)
        {
            _miniTimer = null;
            miniTimer.Close();
        }
    }

    private void ToggleFlyout(PointInt32 click, bool byKeyboard)
    {
        if (_flyout is not null || Environment.TickCount64 - _flyoutClosedAt < FlyoutReopenDelay)
        {
            _flyout?.Close();
            return;
        }

        // The window gives way, as on the Mac it fades back into the menu bar.
        if (_mainWindow is { IsShowing: true } window)
        {
            window.Hide();
        }

        var flyout = new TrayFlyout(_timer, _settings);
        flyout.Closed += (_, _) =>
        {
            _flyout = null;
            _flyoutClosedAt = Environment.TickCount64;
            ClosedWindows.LetGo();
        };
        flyout.View.OpenWindowRequested += ShowMainWindow;
        flyout.View.SettingsRequested += ShowSettings;
        flyout.View.QuitRequested += Quit;
        _flyout = flyout;
        flyout.Open(_tray.Bounds, click, byKeyboard);
    }

    private void ShowTrayMenu(PointInt32 point, bool byKeyboard)
    {
        _trayMenu ??= new TrayMenu(_timer, ShowMainWindow, ShowSettings, Quit);
        _trayMenu.Show(point, byKeyboard, _settings.Appearance);
    }

    private void ShowSettings()
    {
        // Like the main window, built once and hidden when closed: it has its own title bar too.
        _settingsWindow ??= new SettingsWindow(_timer, _settings, _hotKey);
        _settingsWindow.BringToFront();
    }

    /// <summary>
    /// Several properties change together on each tick and each click; react once, after they all have. Reading the state
    /// once it has settled also skips the instant of "stopped" between time's up and the next phase.
    /// </summary>
    private void ScheduleUpdate(DispatcherQueue queue)
    {
        if (_updatePending)
        {
            return;
        }

        _updatePending = true;
        queue.TryEnqueue(() =>
        {
            _updatePending = false;
            UpdateTray();

            var previous = _settledState;
            _settledState = _timer.State;
            if (_settledState == previous)
            {
                return;
            }

            // While a session is on, Windows mustn't slow kyklos down; stopped or paused, there's nothing to slow.
            PowerThrottling.Allow(_settledState is TimerState.Stopped or TimerState.Paused);

            // Minimal mode swaps the window and the mini timer as sessions start and stop.
            Show(MinimalModeRules.ForStateChange(previous, _settledState, _settings.MinimalMode, _miniTimer is not null));
        });
    }

    private void UpdateTray()
    {
        var (tone, fraction) = TrayIndicator.For(_timer);
        _tray.Show(tone, fraction, _timer.TrayTitle);
    }

    private void OnSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        // A new Minimal mode applies at once, rather than at the next session.
        if (e.PropertyName == nameof(Settings.MinimalMode))
        {
            Show(MinimalModeRules.ForModeChange(_settings.MinimalMode, _timer.State, _miniTimer is not null));
        }
    }

    private void Quit()
    {
        // The last keys typed aren't lost, and a notification left on screen launches kyklos again.
        _timer.SaveTask();
        _notifier.Unregister();
        _hotKey.Dispose();
        _timeJumps.Dispose();
        _newDay.Dispose();
        _tray.Dispose();
        _system.Dispose();
        _flyout?.Close();
        CloseMiniTimer();
        _settingsWindow?.Close();
        _mainWindow?.Close();
        _trayMenu?.Close();
        Exit();
    }
}
