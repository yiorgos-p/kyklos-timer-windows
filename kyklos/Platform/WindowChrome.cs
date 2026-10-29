using Microsoft.UI.Windowing;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

namespace Kyklos.Platform;

/// <summary>Window frame details WinUI doesn't cover.</summary>
internal static class WindowChrome
{
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
    /// Asks Windows for its own rounded corners, border and shadow on a window without a title bar, rather than faking a
    /// shape, whose edges come out jagged.
    /// </summary>
    public static unsafe void RoundCorners(nint window)
    {
        var preference = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
        PInvoke.DwmSetWindowAttribute(new HWND(window), DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, &preference, sizeof(DWM_WINDOW_CORNER_PREFERENCE));
    }
}
