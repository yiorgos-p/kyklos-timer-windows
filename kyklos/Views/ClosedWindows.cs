using Microsoft.UI.Dispatching;

namespace Kyklos.Views;

/// <summary>
/// What a closed window (or a Settings page let go of) leaves behind is mostly WinUI's memory, which .NET can't see, so
/// nothing would prompt a garbage collection to hand it back; kyklos allocates so little that it could take hours. So
/// once it has gone, a collection frees it. WinUI's objects go as their .NET wrappers are finalised, which lets go of
/// more wrappers for a second collection. It runs off the UI thread, after the window's last work there.
/// </summary>
internal static class ClosedWindows
{
    public static void LetGo() =>
        DispatcherQueue.GetForCurrentThread().TryEnqueue(DispatcherQueuePriority.Low, () => Task.Run(() =>
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }));
}
