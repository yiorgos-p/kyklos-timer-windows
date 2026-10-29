using System.Diagnostics;

namespace Kyklos.Engine;

/// <summary>The clocks the timer reads, so the tests can move time by hand.</summary>
public interface IClock
{
    /// <summary>Monotonic time: it keeps counting while the PC sleeps and ignores changes to the system clock.</summary>
    TimeSpan Now { get; }

    /// <summary>The wall clock, for showing when a session ends and for knowing which day it is.</summary>
    DateTime LocalNow { get; }
}

public sealed class SystemClock : IClock
{
    // Stopwatch reads QueryPerformanceCounter, which Microsoft documents keeps counting through standby and hibernation
    // and isn't affected by changes to the system time. DateTime.Now would jump with every clock change.
    public TimeSpan Now => Stopwatch.GetElapsedTime(0);

    public DateTime LocalNow => DateTime.Now;
}
