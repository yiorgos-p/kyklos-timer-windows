using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Kyklos.Platform;

/// <summary>Window frame and behaviour details WinUI doesn't cover.</summary>
internal static class WindowChrome
{
    /// <summary>
    /// Clicking the window leaves the keyboard with the app being worked in, as the touch keyboard does. It's still
    /// activated when asked to be, as for typing in it.
    /// </summary>
    public static void DontActivateOnClick(nint window)
    {
        var handle = new HWND(window);
        var style = PInvoke.GetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        PInvoke.SetWindowLong(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, style | (int)WINDOW_EX_STYLE.WS_EX_NOACTIVATE);
    }

    /// <summary>
    /// For a window that's always exactly as big as its content: Windows still draws a maximise button over an extended
    /// title bar, so its command is refused; and should something maximise the window another way (UI Automation invokes
    /// that button directly), it's put straight back. Dispose of the result when the window closes.
    /// </summary>
    public static WindowMessages RefuseMaximize(nint window) => new(window, (message, wParam, _) =>
    {
        if (message == PInvoke.WM_SIZE && wParam == PInvoke.SIZE_MAXIMIZED)
        {
            PInvoke.PostMessage(new HWND(window), PInvoke.WM_SYSCOMMAND, PInvoke.SC_RESTORE, 0);
        }

        return message == PInvoke.WM_SYSCOMMAND && (wParam & 0xFFF0) == PInvoke.SC_MAXIMIZE;
    });

    /// <summary>
    /// Whether a window there would show on any screen. A hidden window isn't moved when its screen goes (a laptop
    /// leaving its dock, say), so one shown again can need placing anew.
    /// </summary>
    public static bool OnScreen(PointInt32 position, SizeInt32 size) =>
        DisplayArea.GetFromRect(new RectInt32(position.X, position.Y, size.Width, size.Height), DisplayAreaFallback.None) is not null;

    /// <summary>
    /// Sizes a window's client area, in pixels. AppWindow.ResizeClient adds a standard title bar's height, but kyklos's
    /// windows have none (the main window draws its own in the content; the flyout and mini timer hide it), so the
    /// window is sized from its actual frame instead.
    /// </summary>
    public static void ResizeClient(AppWindow window, SizeInt32 client)
    {
        var frameWidth = window.Size.Width - window.ClientSize.Width;
        var frameHeight = window.Size.Height - window.ClientSize.Height;
        window.Resize(new SizeInt32(client.Width + frameWidth, client.Height + frameHeight));
    }

    /// <summary>
    /// Moves a window so its client area, the part that shows, starts at this point in pixels. Windows places a window by
    /// bounds that take in invisible borders, so placing by those leaves it further from a screen edge than it looks.
    /// </summary>
    public static void MoveClient(AppWindow window, PointInt32 client)
    {
        var origin = new System.Drawing.Point();
        PInvoke.ClientToScreen(new HWND(Win32Interop.GetWindowFromWindowId(window.Id)), ref origin);
        var position = window.Position;
        window.Move(new PointInt32(client.X - (origin.X - position.X), client.Y - (origin.Y - position.Y)));
    }

    /// <summary>
    /// Asks Windows for its own rounded corners, border and shadow on a window without a title bar, rather than faking a
    /// shape, whose edges come out jagged.
    /// </summary>
    public static unsafe void RoundCorners(nint window)
    {
        var preference = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
        PInvoke.DwmSetWindowAttribute(new HWND(window), DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, &preference, sizeof(DWM_WINDOW_CORNER_PREFERENCE));
    }
}
