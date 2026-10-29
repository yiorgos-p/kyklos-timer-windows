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
        // messages flowing, as the Windows App SDK asks of a single-threaded apartment.
        var arguments = AppInstance.GetCurrent().GetActivatedEventArgs();
        using var done = new ManualResetEvent(false);
        Task.Run(() =>
        {
            running.RedirectActivationToAsync(arguments).AsTask().Wait();
            done.Set();
        });
        PInvoke.CoWaitForMultipleObjects(0, PInvoke.INFINITE, [new HANDLE(done.SafeWaitHandle.DangerousGetHandle())], out _);
    }
}
