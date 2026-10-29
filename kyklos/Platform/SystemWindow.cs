using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Kyklos.Platform;

/// <summary>
/// An invisible window that lives as long as kyklos does, for the Windows messages that never reach a XAML window: the
/// tray icon's clicks, the global shortcut, and what the system broadcasts to top-level windows, like a change of
/// taskbar theme. (A message-only window would miss the broadcasts.)
/// </summary>
internal sealed class SystemWindow : IDisposable
{
    private const string ClassName = "kyklos.system";

    // Kept alive here: Windows holds only a pointer to it.
    private readonly WNDPROC _procedure;

    public unsafe SystemWindow()
    {
        _procedure = Procedure;
        var instance = PInvoke.GetModuleHandle((string?)null);

        fixed (char* name = ClassName)
        {
            var windowClass = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = _procedure,
                hInstance = (HINSTANCE)instance.DangerousGetHandle(),
                lpszClassName = name,
            };
            PInvoke.RegisterClassEx(windowClass);

            Handle = PInvoke.CreateWindowEx(
                WINDOW_EX_STYLE.WS_EX_TOOLWINDOW, name, null, WINDOW_STYLE.WS_POPUP, 0, 0, 0, 0,
                HWND.Null, HMENU.Null, (HINSTANCE)instance.DangerousGetHandle(), null);
        }
    }

    /// <summary>Every message the window receives: the message, its wParam and its lParam.</summary>
    public event Action<uint, nuint, nint>? Received;

    public HWND Handle { get; }

    public void Dispose() => PInvoke.DestroyWindow(Handle);

    private LRESULT Procedure(HWND window, uint message, WPARAM wParam, LPARAM lParam)
    {
        Received?.Invoke(message, wParam.Value, lParam.Value);
        return PInvoke.DefWindowProc(window, message, wParam, lParam);
    }
}
