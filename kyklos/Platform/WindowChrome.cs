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
