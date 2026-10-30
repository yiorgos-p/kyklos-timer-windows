using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Kyklos;

/// <summary>
/// Starts kyklos, once. Launched again while it runs (from Start, or a pinned taskbar icon), it hands the launch to the
/// copy that's running, which shows its window, and leaves.
/// </summary>
public static class Program
{
    /// <summary>A handover that hasn't finished by then never will: the copy that's running is quitting, or stuck.</summary>
    private const uint HandOverTimeout = 10_000;

    [STAThread]
    public static void Main()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        var main = AppInstance.FindOrRegisterForKey("kyklos");
        if (!main.IsCurrent)
        {
            HandOver(main);
            return;
        }

        Application.Start(_ =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            new App();
        });
    }

    private static void HandOver(AppInstance running)
    {
        // This launch came from the user, so it may bring a window to the front; the running copy shows its own.
        PInvoke.AllowSetForegroundWindow(running.ProcessId);

        // The handover has to finish before this copy ends. It runs on another thread while this one waits with COM
        // messages flowing, as the Windows App SDK asks of a single-threaded apartment. (The event isn't disposed: after a
        // wait that gave up, the handover can still set it.)
        var arguments = AppInstance.GetCurrent().GetActivatedEventArgs();
        var done = new ManualResetEvent(false);
        Task.Run(() =>
        {
            try
            {
                running.RedirectActivationToAsync(arguments).AsTask().Wait();
            }
            catch (Exception)
            {
                // The running copy went away meanwhile. There's no window to show, and this copy leaves all the same;
                // launched again, it starts as the only one.
            }
            finally
            {
                done.Set();
            }
        });
        PInvoke.CoWaitForMultipleObjects(0, HandOverTimeout, [new HANDLE(done.SafeWaitHandle.DangerousGetHandle())], out _);
    }
}
