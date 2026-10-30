using Microsoft.UI.Dispatching;

namespace Kyklos.Views;

/// <summary>
/// What a closed window (or a Settings page let go of) leaves behind is mostly WinUI's memory, which .NET can't see, so
/// nothing would prompt a garbage collection to hand it back; kyklos allocates so little that it could take hours. One
/// collection, once it has gone, frees it straight away.
/// </summary>
internal static class ClosedWindows
{
    public static void LetGo() =>
        DispatcherQueue.GetForCurrentThread().TryEnqueue(DispatcherQueuePriority.Low, () => GC.Collect());
}
