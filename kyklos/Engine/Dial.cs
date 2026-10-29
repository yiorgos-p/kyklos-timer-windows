namespace Kyklos.Engine;

/// <summary>The 60-minute dial's geometry.</summary>
public static class Dial
{
    /// <summary>
    /// Turns a point on the dial into minutes: 12 o'clock is 60, and each minute mark is one minute. Once a drag
    /// reaches 60 or 1 it stays there rather than jumping across 12 o'clock.
    /// </summary>
    public static int MinutesAt(double x, double y, double center, int? lastDragMinutes)
    {
        var degrees = Math.Atan2(y - center, x - center) * 180 / Math.PI + 90;
        if (degrees < 0)
        {
            degrees += 360;
        }

        // Halves round away from zero, as Swift's rounded() does on the Mac; Math.Round would round them to even.
        var minutes = (int)Math.Round(degrees / 6, MidpointRounding.AwayFromZero);
        if (minutes == 0)
        {
            minutes = 60;
        }

        if (lastDragMinutes is { } last && Math.Abs(minutes - last) > 30)
        {
            minutes = last > 30 ? 60 : 1;
        }

        return Math.Max(1, minutes);
    }
}
