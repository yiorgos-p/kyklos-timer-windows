using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace Kyklos.Platform;

/// <summary>
/// Lets kyklos see a window's messages before Windows handles them, for the few things WinUI doesn't expose. Return
/// true from the handler to say a message was dealt with.
/// </summary>
public sealed class WindowMessages : IDisposable
{
    public delegate bool Handler(uint message, nuint wParam, nint lParam);

    // Kept alive here: Windows holds only a pointer to it.
    private readonly SUBCLASSPROC _procedure;
    private readonly HWND _window;
    private readonly Handler _handler;

    public WindowMessages(nint window, Handler handler)
    {
        _window = new HWND(window);
        _handler = handler;
        _procedure = Procedure;
        PInvoke.SetWindowSubclass(_window, _procedure, 1, 0);
    }

    public void Dispose() => PInvoke.RemoveWindowSubclass(_window, _procedure, 1);

    private LRESULT Procedure(HWND window, uint message, WPARAM wParam, LPARAM lParam, nuint id, nuint data) =>
        _handler(message, wParam.Value, lParam.Value)
            ? new LRESULT(0)
            : PInvoke.DefSubclassProc(window, message, wParam, lParam);
}
