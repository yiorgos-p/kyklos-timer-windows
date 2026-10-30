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
/// open from, so a one-pixel window sits under the pointer while it's open.
/// </summary>
internal static class TrayMenu
{
    public static unsafe void Show(PointInt32 point, bool byKeyboard, TimerViewModel timer, AppTheme theme, Action openWindow, Action openSettings, Action quit)
    {
        var root = new Grid { RequestedTheme = theme.ToElementTheme() };
        var host = new Window { Content = root, Title = "kyklos" };

        // Windows gives windows a minimum size; this one has to stay a single pixel.
        var messages = new WindowMessages(Win32Interop.GetWindowFromWindowId(host.AppWindow.Id), (message, _, lParam) =>
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
        host.AppWindow.SetPresenter(presenter);
        host.AppWindow.IsShownInSwitchers = false;
        host.AppWindow.MoveAndResize(new RectInt32(point.X, point.Y, 1, 1));
        host.Closed += (_, _) =>
        {
            messages.Dispose();
            ClosedWindows.LetGo();
        };

        var finished = timer.State == TimerState.Finished;
        var main = new MenuFlyoutItem
        {
            Text = timer.PrimaryActionTitle,
            Icon = finished
                ? PhaseIcons.Create(timer.NextPhase)
                : new FontIcon { Glyph = timer.State == TimerState.Running ? Glyphs.PauseBold : Glyphs.PlaySolid },
        };
        main.Click += (_, _) => timer.ToggleTimer();

        var reset = new MenuFlyoutItem { Text = timer.ResetActionTitle, Icon = new FontIcon { Glyph = finished ? Glyphs.StopSolid : Glyphs.Undo } };
        reset.Click += (_, _) => timer.ResetTimer();

        var open = new MenuFlyoutItem { Text = "Open kyklos" };
        open.Click += (_, _) => openWindow();

        var settings = new MenuFlyoutItem { Text = "Settings", Icon = new FontIcon { Glyph = Glyphs.Settings } };
        settings.Click += (_, _) => openSettings();

        var quitItem = new MenuFlyoutItem { Text = "Quit kyklos", Icon = new FontIcon { Glyph = Glyphs.PowerButton } };
        quitItem.Click += (_, _) => quit();

        var menu = new MenuFlyout
        {
            ShouldConstrainToRootBounds = false,
            Items = { main, reset, new MenuFlyoutSeparator(), open, settings, new MenuFlyoutSeparator(), quitItem },
        };
        menu.Closed += (_, _) => host.Close();

        // Its window never sees the click, so WinUI opens the menu as if from the keyboard, highlighting the first item.
        // From a right click it should open as Windows' own menus do, with nothing highlighted. The menu passes the focus
        // on to its first item the way it got it, so it's given it as if from the pointer; the arrow keys still work.
        if (!byKeyboard)
        {
            menu.Opened += (_, _) => (FocusManager.GetFocusedElement(root.XamlRoot) as MenuFlyoutPresenter)?.Focus(FocusState.Pointer);
        }

        root.Loaded += (_, _) => menu.ShowAt(root, new FlyoutShowOptions { Position = new Point(0, 0) });
        host.Activate();
    }
}
