namespace Kyklos.Engine;

/// <summary>
/// Calls back when a new day begins, so today's count of sessions starts again from zero at midnight, as the Mac's does
/// when the calendar day changes. It's one timer for the next local midnight, set again each time.
/// </summary>
public sealed class NewDay : IDisposable
{
    /// <summary>A moment past midnight, so the date has certainly changed when it fires.</summary>
    private static readonly TimeSpan Margin = TimeSpan.FromSeconds(1);

    private readonly IClock _clock;
    private readonly IScheduler _scheduler;
    private readonly Action _begun;
    private IDisposable? _pending;

    public NewDay(IClock clock, IScheduler scheduler, Action begun)
    {
        _clock = clock;
        _scheduler = scheduler;
        _begun = begun;
        Arm();
    }

    /// <summary>After a sleep or a change of clock or time zone, midnight may have passed or moved.</summary>
    public void Recheck()
    {
        _begun();
        Arm();
    }

    public void Dispose() => _pending?.Dispose();

    /// <summary>
    /// The real time from now to the next midnight in this time zone. On the days the clocks change that's 23 or 25
    /// hours, not the 24 the two local times are apart; and where the change skips midnight itself, the new day begins
    /// at the first time there is.
    /// </summary>
    public static TimeSpan UntilMidnight(DateTime now, TimeZoneInfo zone)
    {
        var midnight = now.Date.AddDays(1);
        while (zone.IsInvalidTime(midnight))
        {
            midnight = midnight.AddMinutes(15);
        }

        return TimeZoneInfo.ConvertTimeToUtc(midnight, zone) - TimeZoneInfo.ConvertTimeToUtc(now, zone);
    }

    private void Arm()
    {
        _pending?.Dispose();
        _pending = _scheduler.Schedule(UntilMidnight(_clock.LocalNow, TimeZoneInfo.Local) + Margin, () =>
        {
            _begun();
            Arm();
        });
    }
}
