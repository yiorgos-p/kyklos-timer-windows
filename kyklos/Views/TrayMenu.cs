using System.ComponentModel;
using Kyklos.Engine;
using Kyklos.Platform;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Kyklos.Views;

/// <summary>
/// The tray icon's right-click menu, the usual way into a tray app on Windows: the main button's action, Reset or Stop,
/// then Open kyklos, Settings and Quit kyklos. It's a WinUI menu, so it follows kyklos's theme; a menu needs a window to
/// open from, so a one-pixel window sits under the pointer while it's open. The menu and its window are made once and
/// hidden between uses: WinUI never quite frees them, so ones made each time would leave a little behind every time.
/// </summary>
internal sealed class TrayMenu
{
    private readonly TimerViewModel _timer;
    private readonly Window _host;
    private readonly Grid _root = new();
    private readonly MenuFlyout _menu;
    private readonly MenuFlyoutItem _main = new();
    private readonly MenuFlyoutItem _reset = new();
    private readonly WindowMessages _messages;
    private bool _byKeyboard;
    private bool _closed;

    public unsafe TrayMenu(TimerViewModel timer, Action openWindow, Action openSettings, Action quit)
    {
        _timer = timer;
        _host = new Window { Content = _root, Title = "kyklos" };

        // Windows gives windows a minimum size; this one has to stay a single pixel.
        _messages = new WindowMessages(Win32Interop.GetWindowFromWindowId(_host.AppWindow.Id), (message, _, lParam) =>
        {
            if (message != PInvoke.WM_GETMINMAXINFO)
            {
                return false;
            }

            ((MINMAXINFO*)lParam)->ptMinTrackSize = new System.Drawing.Point(1, 1);
            return true;
        });

        // A context menu's presenter, here without even its border.
        var presenter = OverlappedPresenter.CreateForContextMenu();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsAlwaysOnTop = true;
        _host.AppWindow.SetPresenter(presenter);
        _host.AppWindow.IsShownInSwitchers = false;

        _main.Click += (_, _) => _timer.ToggleTimer();
        _reset.Click += (_, _) => _timer.ResetTimer();

        var open = new MenuFlyoutItem { Text = "Open kyklos" };
        open.Click += (_, _) => openWindow();

        var settings = new MenuFlyoutItem { Text = "Settings", Icon = new FontIcon { Glyph = Glyphs.Settings } };
        settings.Click += (_, _) => openSettings();

        var quitItem = new MenuFlyoutItem { Text = "Quit kyklos", Icon = new FontIcon { Glyph = Glyphs.PowerButton } };
        quitItem.Click += (_, _) => quit();

        _menu = new MenuFlyout
        {
            ShouldConstrainToRootBounds = false,
            Items = { _main, _reset, new MenuFlyoutSeparator(), open, settings, new MenuFlyoutSeparator(), quitItem },
        };
        // After Quit kyklos, the menu closes with its window already gone.
        _menu.Closed += (_, _) =>
        {
            _timer.PropertyChanged -= OnTimerChanged;
            if (!_closed)
            {
                _host.AppWindow.Hide();
            }
        };

        // Its window never sees the click, so WinUI opens the menu as if from the keyboard, highlighting the first item.
        // From a right click it should open as Windows' own menus do, with nothing highlighted. The menu passes the focus
        // on to its first item the way it got it, so it's given it as if from the pointer; the arrow keys still work.
        _menu.Opened += (_, _) =>
        {
            // Time can run out while it's open, and Pause then has to become the next phase's button.
            _timer.PropertyChanged += OnTimerChanged;
            if (!_byKeyboard)
            {
                (FocusManager.GetFocusedElement(_root.XamlRoot) as MenuFlyoutPresenter)?.Focus(FocusState.Pointer);
            }
        };

        // The first time, the window has to be up before the menu can open from it.
        _root.Loaded += (_, _) => Open();
    }

    public void Show(PointInt32 point, bool byKeyboard, AppTheme theme)
    {
        _byKeyboard = byKeyboard;
        _root.RequestedTheme = theme.ToElementTheme();
        _host.AppWindow.MoveAndResize(new RectInt32(point.X, point.Y, 1, 1));
        ShowTimerItems();

        _host.Activate();
        if (_root.IsLoaded)
        {
            Open();
        }
    }

    /// <summary>On Quit.</summary>
    public void Close()
    {
        _closed = true;
        _messages.Dispose();
        _host.Close();
    }

    private void Open() => _menu.ShowAt(_root, new FlyoutShowOptions { Position = new Point(0, 0) });

    /// <summary>The first two items follow the timer.</summary>
    private void ShowTimerItems()
    {
        var finished = _timer.State == TimerState.Finished;
        _main.Text = _timer.PrimaryActionTitle;
        _main.Icon = finished
            ? PhaseIcons.Create(_timer.NextPhase)
            : new FontIcon { Glyph = _timer.State == TimerState.Running ? Glyphs.PauseBold : Glyphs.PlaySolid };
        _reset.Text = _timer.ResetActionTitle;
        _reset.Icon = new FontIcon { Glyph = finished ? Glyphs.StopSolid : Glyphs.Undo };
    }

    // Each tick says the time changed; only a new state or phase changes the items.
    private void OnTimerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TimerViewModel.State) or nameof(TimerViewModel.Phase))
        {
            ShowTimerItems();
        }
    }
}
