using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace Kyklos.Platform;

/// <summary>
/// Says when a window comes to the front, whichever app it belongs to. A window is only told it has lost the front if
/// it had it, and Windows doesn't always let a tray app's window take it. Dispose of it to stop listening.
/// </summary>
internal sealed class ForegroundChanges : IDisposable
{
    // Kept alive here: Windows holds only a pointer to it.
    private readonly WINEVENTPROC _procedure;
    private readonly UnhookWinEventSafeHandle _hook;

    /// <summary>Calls <paramref name="changed"/> on this thread with each new front window.</summary>
    public ForegroundChanges(Action<HWND> changed)
    {
        _procedure = (_, _, window, _, _, _, _) => changed(window);
        _hook = PInvoke.SetWinEventHook(
            PInvoke.EVENT_SYSTEM_FOREGROUND, PInvoke.EVENT_SYSTEM_FOREGROUND, null, _procedure, 0, 0, PInvoke.WINEVENT_OUTOFCONTEXT);
    }

    public void Dispose() => _hook.Dispose();
}
