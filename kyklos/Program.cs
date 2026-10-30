using Kyklos.Platform;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.Win32;

namespace Kyklos;

/// <summary>
/// Starts kyklos, once. Launched again while it runs (from Start, a pinned taskbar icon, or a copy of kyklos.exe saved
/// somewhere else), it asks the copy that's running to show its window, and leaves.
/// </summary>
public static class Program
{
    /// <summary>
    /// Held as long as kyklos runs, in this sign-in session. It's named for kyklos rather than for where its exe is,
    /// as the Windows App SDK's own single-instance key is: with that, a copy started from another folder ran alongside.
    /// </summary>
    private const string RunningName = @"Local\io.github.yiorgos-p.Kyklos";

    /// <summary>How long a second launch looks for the running copy's window: that copy may still be starting.</summary>
    private static readonly TimeSpan HandOverTimeout = TimeSpan.FromSeconds(5);

    // Kept for the life of the process: let go, it would say kyklos had ended.
    private static Mutex? _running;

    [STAThread]
    public static void Main()
    {
        _running = new Mutex(false, RunningName, out var first);
        if (!first)
        {
            HandOver();
            return;
        }

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            new App();
        });
    }

    private static unsafe void HandOver()
    {
        var giveUp = DateTime.UtcNow + HandOverTimeout;
        while (DateTime.UtcNow < giveUp)
        {
            var window = PInvoke.FindWindow(SystemWindow.ClassName, null);
            if (!window.IsNull)
            {
                // This launch came from the user, so it may bring a window to the front; the running copy shows its own.
                uint process;
                PInvoke.GetWindowThreadProcessId(window, &process);
                PInvoke.AllowSetForegroundWindow(process);
                PInvoke.PostMessage(window, SystemWindow.ShowMessage, 0, 0);
                return;
            }

            // Not there yet, or any more: the running copy is starting, or quitting. If it was quitting, a launch after
            // it has gone starts as the only one.
            Thread.Sleep(100);
        }
    }
}
