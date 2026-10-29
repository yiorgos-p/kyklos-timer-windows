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

    private void Arm()
    {
        _pending?.Dispose();
        var now = _clock.LocalNow;
        _pending = _scheduler.Schedule(now.Date.AddDays(1) - now + Margin, () =>
        {
            _begun();
            Arm();
        });
    }
}
