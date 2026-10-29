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
    public void AFocusFinishedAfterMidnightCountsForTheNewDay()
    {
        using var test = new TestTimer(settings =>
        {
            settings.SessionsDay = "2026-10-29";
            settings.SessionsToday = 3;
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
