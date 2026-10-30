namespace Kyklos.Engine;

/// <summary>What the tray icon shows: the kyklos glyph while stopped, or a pie of the time left in one of these colours.</summary>
public enum TrayTone
{
    Glyph,
    Green,
    Yellow,
    Red,
    Paused,
}

/// <summary>The tray icon's progress ring, worked out like the Mac's menu bar circle.</summary>
public static class TrayIndicator
{
    /// <summary>
    /// The pie covers the fraction of the session left: green, then yellow with a third left, red for the last tenth,
    /// grey while paused, and all red once time's up.
    /// </summary>
    public static (TrayTone Tone, double Fraction) For(TimerViewModel timer) => timer.State switch
    {
        TimerState.Stopped => (TrayTone.Glyph, 0),
        TimerState.Paused => (TrayTone.Paused, timer.Progress),
        TimerState.Finished => (TrayTone.Red, 1),
        _ => (timer.Progress > 1.0 / 3 ? TrayTone.Green : timer.Progress > 0.1 ? TrayTone.Yellow : TrayTone.Red, timer.Progress),
    };

    /// <summary>The icon is only redrawn when this changes: at tray size, 64 steps round the circle is finer than the eye sees.</summary>
    public static int Step(double fraction) => (int)Math.Round(fraction * 64);

    /// <summary>Windows keeps 128 UTF-16 units of a tray icon's tooltip, the end marker among them.</summary>
    public const int TooltipLength = 127;

    /// <summary>
    /// The icon's tooltip: "kyklos", then on a second line the time and task, as the Mac shows them in its menu bar. The
    /// task is already shortened to 24 characters, but those can still come to more than Windows keeps.
    /// </summary>
    public static string Tooltip(string title) => Utf16Limit.Fit("kyklos\n" + title, TooltipLength);
}
