using Kyklos.Engine;
using Microsoft.UI.Dispatching;

namespace Kyklos.Platform;

/// <summary>Runs the engine's one-shot timers on the UI thread.</summary>
public sealed class DispatcherScheduler(DispatcherQueue queue) : IScheduler
{
    public IDisposable Schedule(TimeSpan delay, Action action)
    {
        var timer = queue.CreateTimer();
        timer.Interval = delay;
        timer.IsRepeating = false;
        timer.Tick += (_, _) => action();
        timer.Start();
        return new Cancellation(timer);
    }

    private sealed class Cancellation(DispatcherQueueTimer timer) : IDisposable
    {
        public void Dispose() => timer.Stop();
    }
}
