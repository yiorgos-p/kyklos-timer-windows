namespace Kyklos.Engine;

/// <summary>"Minimal mode" in Settings: when the small floating mini timer stands in for the window.</summary>
public enum MinimalMode
{
    Off,

    /// <summary>"On start": the mini timer while a session is on, the window otherwise.</summary>
    OnStart,

    /// <summary>"Always": the mini timer from launch, back with every session even after it's closed.</summary>
    Always,
}

/// <summary>Where the timer is shown: the main window, or the mini timer.</summary>
public enum TimerSurface
{
    MainWindow,
    MiniTimer,
}

/// <summary>When Minimal mode swaps the window for the mini timer and back, as the Mac does.</summary>
public static class MinimalModeRules
{
    public static TimerSurface AtLaunch(MinimalMode mode) =>
        mode == MinimalMode.Always ? TimerSurface.MiniTimer : TimerSurface.MainWindow;

    /// <summary>
    /// What a change of the timer's state calls for, if anything. A session starting brings the mini timer, even one closed
    /// with its ×; resuming doesn't. Stopping brings the window back in "On start". Pass the states as they settle: going
    /// from time's up to the next phase passes through stopped for an instant, which mustn't flash the window.
    /// </summary>
    public static TimerSurface? ForStateChange(TimerState previous, TimerState state, MinimalMode mode, bool miniTimerShowing)
    {
        if (state == TimerState.Running && (previous is TimerState.Stopped or TimerState.Finished) && mode != MinimalMode.Off)
        {
            return TimerSurface.MiniTimer;
        }

        if (state == TimerState.Stopped && previous != TimerState.Stopped && mode == MinimalMode.OnStart && miniTimerShowing)
        {
            return TimerSurface.MainWindow;
        }

        return null;
    }

    /// <summary>What changing the setting calls for: it applies at once, rather than at the next session.</summary>
    public static TimerSurface? ForModeChange(MinimalMode mode, TimerState state, bool miniTimerShowing) => mode switch
    {
        MinimalMode.Always => TimerSurface.MiniTimer,
        MinimalMode.OnStart when state != TimerState.Stopped => TimerSurface.MiniTimer,
        MinimalMode.Off when miniTimerShowing => TimerSurface.MainWindow,
        _ => null,
    };
}
