namespace Kyklos.Tests;

public class SessionTests
{
    [Fact]
    public void TheCountStartsAgainAtMidnight()
    {
        using var test = new TestTimer();
        test.Clock.LocalNow = new DateTime(2026, 10, 29, 22, 0, 0);
        test.Finish();
        test.Timer.SetPhase(TimerPhase.Focus);
        test.Finish();
        Assert.Equal(2, test.Timer.CompletedFocusToday);

        test.Clock.LocalNow = new DateTime(2026, 10, 30, 0, 0, 1);
        test.Timer.LoadTodaysSessions();

        Assert.Equal(0, test.Timer.CompletedFocusToday);
        Assert.Equal(0, SessionDots.Filled(test.Timer.CompletedFocusToday, test.Settings.SessionsPerLongBreak));
    }

    [Fact]
    public void AtMidnightTheCountGoesBackToZeroOnItsOwn()
    {
        using var test = new TestTimer();
        test.Clock.LocalNow = new DateTime(2026, 10, 29, 23, 0, 0);
        using var newDay = new NewDay(test.Clock, test.Scheduler, test.Timer.LoadTodaysSessions);
        test.Finish();
        Assert.Equal(1, test.Timer.CompletedFocusToday);

        test.Advance(34 * 60);
        Assert.Equal(1, test.Timer.CompletedFocusToday);

        // Midnight, and a second for good measure.
        test.Advance(60 + 1);
        Assert.Equal(0, test.Timer.CompletedFocusToday);

        // And again the night after.
        test.Finish();
        test.Advance(24 * 60 * 60 - 25 * 60);
        Assert.Equal(0, test.Timer.CompletedFocusToday);
    }

    [Fact]
    public void AfterASleepPastMidnightTheCountIsTodays()
    {
        using var test = new TestTimer();
        test.Clock.LocalNow = new DateTime(2026, 10, 29, 22, 0, 0);
        using var newDay = new NewDay(test.Clock, test.Scheduler, test.Timer.LoadTodaysSessions);
        test.Finish();
        test.Timer.ResetTimer();

        // A clock put forward a day: the timer for midnight still waits, so checking on the change catches it.
        test.Clock.LocalNow = new DateTime(2026, 10, 30, 9, 0, 0);
        newDay.Recheck();

        Assert.Equal(0, test.Timer.CompletedFocusToday);
    }

    [Fact]
    public void AFocusFinishedAfterMidnightCountsForTheNewDay()
    {
        using var test = new TestTimer(settings =>
        {
            settings.SetSessions("2026-10-29", 3);
        });
        Assert.Equal(3, test.Timer.CompletedFocusToday);

        // 25 minutes from 23:50 ends at 00:15.
        test.Clock.LocalNow = new DateTime(2026, 10, 29, 23, 50, 0);
        test.Finish();

        Assert.Equal(1, test.Timer.CompletedFocusToday);
        Assert.Equal("2026-10-30", test.Settings.SessionsDay);
        Assert.Equal(1, test.Settings.SessionsToday);
    }

    [Fact]
    public void ResettingTodaysSessionsStartsFromZero()
    {
        using var test = new TestTimer();
        test.Finish();

        test.Timer.ResetTodaysSessions();

        Assert.Equal(0, test.Timer.CompletedFocusToday);
        Assert.Equal(0, test.Settings.SessionsToday);
    }

    [Fact]
    public void AResetCountMidRoundMakesTheNextBreakShort()
    {
        using var test = new TestTimer();

        for (var session = 1; session < 4; session++)
        {
            test.Finish();
            test.Timer.SetPhase(TimerPhase.Focus);
        }

        test.Finish();
        Assert.Equal(TimerPhase.LongBreak, test.Timer.NextPhase);

        test.Timer.ResetTodaysSessions();
        Assert.Equal(TimerPhase.ShortBreak, test.Timer.NextPhase);
    }

    [Fact]
    public void MidnightIsReallyAsFarAsTheClocksMakeIt()
    {
        // Summer time from the last Sunday in March at 03:00 to the last Sunday in October at 04:00, as in Athens.
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 3, 0, 0), 3, 5, DayOfWeek.Sunday),
            TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 4, 0, 0), 10, 5, DayOfWeek.Sunday));
        var zone = TimeZoneInfo.CreateCustomTimeZone("Test", TimeSpan.FromHours(2), "Test", "Test", "Test summer", [rule]);

        Assert.Equal(TimeSpan.FromMinutes(10), NewDay.UntilMidnight(new DateTime(2026, 10, 29, 23, 50, 0), zone));
        Assert.Equal(TimeSpan.FromHours(23), NewDay.UntilMidnight(new DateTime(2026, 3, 29, 0, 0, 0), zone));
        Assert.Equal(TimeSpan.FromHours(25), NewDay.UntilMidnight(new DateTime(2026, 10, 25, 0, 0, 0), zone));
    }

    [Fact]
    public void WhereTheClocksSkipMidnightTheDayBeginsAfterIt()
    {
        // Summer time from 1 September at midnight: that day's clocks go from 23:59 straight to 01:00.
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 0, 0, 0), 9, 1),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 0, 0, 0), 4, 1));
        var zone = TimeZoneInfo.CreateCustomTimeZone("Test", TimeSpan.FromHours(-4), "Test", "Test", "Test summer", [rule]);

        Assert.Equal(TimeSpan.FromHours(1), NewDay.UntilMidnight(new DateTime(2026, 8, 31, 23, 0, 0), zone));
    }

    [Theory]
    [InlineData(0, 4, 0)]
    [InlineData(1, 4, 1)]
    [InlineData(4, 4, 4)]
    [InlineData(5, 4, 1)]
    [InlineData(8, 4, 4)]
    [InlineData(3, 2, 1)]
    public void DotsFillOneRoundAtATime(int completed, int perCycle, int filled) =>
        Assert.Equal(filled, SessionDots.Filled(completed, perCycle));
}
