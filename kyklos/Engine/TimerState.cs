namespace Kyklos.Engine;

/// <summary>Where the timer is: ready to start, counting down, paused, or past zero.</summary>
public enum TimerState
{
    Stopped,
    Running,
    Paused,

    /// <summary>The countdown reached zero, and overtime is counting until the next phase is started.</summary>
    Finished,
}
