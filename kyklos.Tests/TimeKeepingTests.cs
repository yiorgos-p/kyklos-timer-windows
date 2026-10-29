using System.Globalization;

namespace Kyklos.Tests;

public class TimeKeepingTests
{
    [Fact]
    public void AFreshFocusReadsTwentyFiveMinutes()
    {
        using var test = new TestTimer();

        Assert.Equal("25:00", test.Timer.DisplayTime);
        Assert.Equal(TimerState.Stopped, test.Timer.State);
        Assert.Equal("ready", test.Timer.StatusCaption);
    }

    [Fact]
    public void StartingShows2459OneSecondLater()
    {
        using var test = new TestTimer();

        test.Timer.StartTimer();
        Assert.Equal("25:00", test.Timer.DisplayTime);

        test.Advance(0.999);
        Assert.Equal("25:00", test.Timer.DisplayTime);

        test.Advance(0.001);
        Assert.Equal("24:59", test.Timer.DisplayTime);
    }

    // Real timers fire a little late, and now and then a hair early. Neither may show a second twice or skip one.
    [Theory]
    [InlineData(0.0)]
    [InlineData(16.0)]
    [InlineData(-0.5)]
    public void CountsDownEachSecondOnce(double latenessInMilliseconds)
    {
        using var test = new TestTimer();
        test.Scheduler.Lateness = TimeSpan.FromMilliseconds(latenessInMilliseconds);
        var shown = new List<int>();
        test.Timer.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TimerViewModel.RemainingTime))
            {
                shown.Add(test.Timer.RemainingTime);
            }
        };

        test.Timer.StartTimer();
        test.Advance(1499.5);

        Assert.Equal("00:01", test.Timer.DisplayTime);
        Assert.Equal(1499, test.Scheduler.Runs);

        test.Advance(1);

        Assert.Equal(TimerState.Finished, test.Timer.State);
        Assert.Equal(Enumerable.Range(0, 1500).Reverse(), shown);
    }

    [Fact]
    public void PausingRoundsUpAndResumingContinuesFromExactlyThere()
    {
        using var test = new TestTimer();
        test.Timer.StartTimer();
        test.Advance(46.4);

        test.Timer.PauseTimer();

        // 24:13.6 left.
        Assert.Equal("24:14", test.Timer.DisplayTime);
        Assert.Equal("paused", test.Timer.StatusCaption);

        test.Advance(600);
        Assert.Equal("24:14", test.Timer.DisplayTime);

        test.Timer.ToggleTimer();
        Assert.Equal(TimerState.Running, test.Timer.State);
        Assert.Equal("24:14", test.Timer.DisplayTime);

        test.Advance(0.6);
        Assert.Equal("24:13", test.Timer.DisplayTime);

        test.Advance(1452.9);
        Assert.Equal(TimerState.Running, test.Timer.State);

        test.Advance(0.1);
        Assert.Equal(TimerState.Finished, test.Timer.State);
    }

    [Fact]
    public void NothingRunsWhileStoppedOrPaused()
    {
        using var test = new TestTimer();
        Assert.Equal(0, test.Scheduler.Pending);

        test.Timer.StartTimer();
        Assert.Equal(1, test.Scheduler.Pending);

        test.Timer.PauseTimer();
        Assert.Equal(0, test.Scheduler.Pending);

        test.Timer.ResetTimer();
        Assert.Equal(0, test.Scheduler.Pending);
    }

    [Fact]
    public void TimesUpTheInstantZeroWouldShowThenCountsOvertime()
    {
        using var test = new TestTimer();
        test.Timer.SetDuration(1);
        test.Timer.StartTimer();

        test.Advance(59.5);
        Assert.Equal("00:01", test.Timer.DisplayTime);
        Assert.Equal(TimerState.Running, test.Timer.State);

        test.Advance(0.5);
        Assert.Equal(TimerState.Finished, test.Timer.State);
        Assert.Equal("+00:00", test.Timer.DisplayTime);
        Assert.Equal("time's up", test.Timer.StatusCaption);
        Assert.Equal(1.0, test.Timer.DialFraction);
        Assert.Single(test.Alarm.Plays);
        Assert.Single(test.Notifier.Posted);

        test.Advance(0.999);
        Assert.Equal("+00:00", test.Timer.DisplayTime);

        test.Advance(0.001);
        Assert.Equal("+00:01", test.Timer.DisplayTime);
    }

    [Fact]
    public void OvertimeMinutesDontWrap()
    {
        using var test = new TestTimer();
        test.Finish();

        test.Advance(125 * 60 + 3);

        Assert.Equal("+125:03", test.Timer.DisplayTime);
    }

    [Fact]
    public void CatchesUpOnWake()
    {
        using var test = new TestTimer();
        test.Timer.SetDuration(5);
        test.Timer.StartTimer();

        test.Scheduler.Sleep(TimeSpan.FromMinutes(2));
        test.Timer.CatchUp();

        Assert.Equal("03:00", test.Timer.DisplayTime);
    }

    [Fact]
    public void SleepingThroughTimesUpRingsOnWakeAndThenAtTheNextFullMinute()
    {
        using var test = new TestTimer();
        test.Timer.SetDuration(2);
        test.Timer.StartTimer();

        test.Scheduler.Sleep(TimeSpan.FromMinutes(5));
        test.Timer.CatchUp();

        Assert.Equal(TimerState.Finished, test.Timer.State);
        Assert.Equal("+03:00", test.Timer.DisplayTime);

        // As on the Mac, the time's-up ring is restarted at once for the third minute, so it sounds as one ring.
        var ringsOnWake = test.Alarm.Plays.Count;
        Assert.InRange(ringsOnWake, 1, 2);

        test.Advance(59);
        Assert.Equal(ringsOnWake, test.Alarm.Plays.Count);

        test.Advance(1);
        Assert.Equal(ringsOnWake + 1, test.Alarm.Plays.Count);
    }

    [Fact]
    public void TheCaptionShowsWhenTheSessionEndsInTheUsersTimeFormat()
    {
        using var test = new TestTimer();
        test.Clock.LocalNow = new DateTime(2026, 10, 29, 14, 10, 0);
        var culture = CultureInfo.CurrentCulture;

        try
        {
            test.Timer.StartTimer();

            CultureInfo.CurrentCulture = new CultureInfo("en-GB", useUserOverride: false);
            Assert.Equal("ends 14:35", test.Timer.StatusCaption);

            var american = new CultureInfo("en-US", useUserOverride: false);
            CultureInfo.CurrentCulture = american;
            Assert.Equal("ends " + new DateTime(2026, 10, 29, 14, 35, 0).ToString("t", american), test.Timer.StatusCaption);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }
}
