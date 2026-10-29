namespace Kyklos.Tests;

public class PhaseTests
{
    [Fact]
    public void AFinishedFocusIsCountedAndOffersAShortBreak()
    {
        using var test = new TestTimer();

        test.Finish();

        Assert.Equal(1, test.Timer.CompletedFocusToday);
        Assert.Equal(TimerPhase.ShortBreak, test.Timer.NextPhase);
        Assert.Equal(1, test.Settings.SessionsToday);
        Assert.Equal("2026-10-29", test.Settings.SessionsDay);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void EveryNthFocusOffersALongBreak(int sessionsPerLongBreak)
    {
        using var test = new TestTimer(settings => settings.SessionsPerLongBreak = sessionsPerLongBreak);

        for (var session = 1; session < sessionsPerLongBreak; session++)
        {
            test.Finish();
            Assert.Equal(TimerPhase.ShortBreak, test.Timer.NextPhase);
            test.Timer.SetPhase(TimerPhase.Focus);
        }

        // Before it has finished, the session that completes the round already counts toward it.
        test.Timer.StartTimer();
        Assert.Equal(TimerPhase.LongBreak, test.Timer.NextPhase);

        test.Advance(25 * 60);
        Assert.Equal(sessionsPerLongBreak, test.Timer.CompletedFocusToday);
        Assert.Equal(TimerPhase.LongBreak, test.Timer.NextPhase);
    }

    [Fact]
    public void AfterABreakComesFocus()
    {
        using var test = new TestTimer();
        test.Timer.SetPhase(TimerPhase.LongBreak);

        test.Finish();

        Assert.Equal(TimerPhase.Focus, test.Timer.NextPhase);
        Assert.Equal(0, test.Timer.CompletedFocusToday);
    }

    [Fact]
    public void ResetWhileFinishedStopsAtTheNextPhase()
    {
        using var test = new TestTimer();
        test.Finish();
        var stops = test.Alarm.Stops;

        test.Timer.ResetTimer();

        Assert.Equal(TimerPhase.ShortBreak, test.Timer.Phase);
        Assert.Equal(TimerState.Stopped, test.Timer.State);
        Assert.Equal("05:00", test.Timer.DisplayTime);
        Assert.Equal(stops + 1, test.Alarm.Stops);
        Assert.Equal(0, test.Scheduler.Pending);
    }

    [Fact]
    public void StartWhileFinishedStartsTheNextPhase()
    {
        using var test = new TestTimer();
        test.Finish();

        test.Timer.ToggleTimer();

        Assert.Equal(TimerPhase.ShortBreak, test.Timer.Phase);
        Assert.Equal(TimerState.Running, test.Timer.State);
        Assert.Equal(5 * 60, test.Timer.SessionLength);

        test.Advance(1);
        Assert.Equal("04:59", test.Timer.DisplayTime);
    }

    [Fact]
    public void PickingAPhaseWhileRunningStopsAndLoadsItsPreset()
    {
        using var test = new TestTimer();
        test.Timer.StartTimer();
        test.Advance(10);

        test.Timer.SetPhase(TimerPhase.LongBreak);

        Assert.Equal(TimerState.Stopped, test.Timer.State);
        Assert.Equal(TimerPhase.LongBreak, test.Timer.Phase);
        Assert.Equal("10:00", test.Timer.DisplayTime);
        Assert.Equal(1, test.Alarm.Stops);
        Assert.Equal(0, test.Scheduler.Pending);
    }

    [Fact]
    public void ResetGoesBackToTheLengthThatWasSet()
    {
        using var test = new TestTimer();
        test.Timer.SetDuration(12);
        test.Timer.StartTimer();
        test.Advance(30);

        test.Timer.ResetTimer();

        Assert.Equal(TimerState.Stopped, test.Timer.State);
        Assert.Equal("12:00", test.Timer.DisplayTime);
    }

    [Fact]
    public void SetDurationClampsAndOnlyWorksWhileStopped()
    {
        using var test = new TestTimer();

        test.Timer.SetDuration(0);
        Assert.Equal(1, test.Timer.TimerDuration);

        test.Timer.SetDuration(75);
        Assert.Equal(60, test.Timer.TimerDuration);
        Assert.Equal("60:00", test.Timer.DisplayTime);

        test.Timer.StartTimer();
        test.Timer.SetDuration(10);
        Assert.Equal(60, test.Timer.TimerDuration);

        test.Timer.PauseTimer();
        test.Timer.SetDuration(10);
        Assert.Equal(60, test.Timer.TimerDuration);
    }

    [Fact]
    public void PresetsComeFromSettings()
    {
        using var test = new TestTimer(settings =>
        {
            settings.StandardTimer = 50;
            settings.ShortBreak = 7;
        });

        Assert.Equal("50:00", test.Timer.DisplayTime);

        test.Timer.SetPhase(TimerPhase.ShortBreak);
        Assert.Equal("07:00", test.Timer.DisplayTime);
    }

    [Fact]
    public void ChangingTheCurrentPresetShowsItAtOnceButOnlyWhileStopped()
    {
        using var test = new TestTimer();

        test.Settings.StandardTimer = 40;
        Assert.Equal("40:00", test.Timer.DisplayTime);

        test.Timer.StartTimer();
        test.Settings.StandardTimer = 30;
        Assert.Equal(TimerState.Running, test.Timer.State);
        Assert.Equal(40 * 60, test.Timer.SessionLength);

        test.Timer.ResetTimer();
        test.Settings.ShortBreak = 9;
        Assert.Equal("40:00", test.Timer.DisplayTime);
    }
}
