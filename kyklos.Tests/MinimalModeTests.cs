namespace Kyklos.Tests;

public class MinimalModeTests
{
    [Theory]
    [InlineData(MinimalMode.Off, TimerSurface.MainWindow)]
    [InlineData(MinimalMode.OnStart, TimerSurface.MainWindow)]
    [InlineData(MinimalMode.Always, TimerSurface.MiniTimer)]
    public void KyklosOpensInTheMiniTimerOnlyInAlways(MinimalMode mode, TimerSurface surface) =>
        Assert.Equal(surface, MinimalModeRules.AtLaunch(mode));

    [Theory]
    [InlineData(MinimalMode.OnStart, TimerSurface.MiniTimer)]
    [InlineData(MinimalMode.Always, TimerSurface.MiniTimer)]
    [InlineData(MinimalMode.Off, null)]
    public void StartingASessionBringsTheMiniTimer(MinimalMode mode, TimerSurface? surface) =>
        Assert.Equal(surface, MinimalModeRules.ForStateChange(TimerState.Stopped, TimerState.Running, mode, miniTimerShowing: false));

    [Fact]
    public void ResumingDoesntBringBackAClosedMiniTimer() =>
        Assert.Null(MinimalModeRules.ForStateChange(TimerState.Paused, TimerState.Running, MinimalMode.Always, miniTimerShowing: false));

    [Fact]
    public void StoppingBringsTheWindowBackOnlyInOnStart()
    {
        Assert.Equal(TimerSurface.MainWindow,
            MinimalModeRules.ForStateChange(TimerState.Running, TimerState.Stopped, MinimalMode.OnStart, miniTimerShowing: true));
        Assert.Null(MinimalModeRules.ForStateChange(TimerState.Running, TimerState.Stopped, MinimalMode.Always, miniTimerShowing: true));

        // A mini timer closed with its × stays closed, and so does the window; the tray brings it back.
        Assert.Null(MinimalModeRules.ForStateChange(TimerState.Paused, TimerState.Stopped, MinimalMode.OnStart, miniTimerShowing: false));
    }

    [Fact]
    public void GoingFromTimesUpToTheNextPhaseKeepsTheMiniTimer()
    {
        using var test = new TestTimer();
        test.Finish();
        var states = new List<TimerState>();
        test.Timer.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TimerViewModel.State))
            {
                states.Add(test.Timer.State);
            }
        };

        test.Timer.ToggleTimer();

        // On the way it's stopped for an instant. Compared once it has settled, it went from time's up to running, so
        // the window doesn't flash up in between.
        Assert.Equal([TimerState.Stopped, TimerState.Running], states);
        Assert.Equal(TimerSurface.MiniTimer,
            MinimalModeRules.ForStateChange(TimerState.Finished, test.Timer.State, MinimalMode.OnStart, miniTimerShowing: true));
    }

    [Fact]
    public void ChangingTheSettingAppliesAtOnce()
    {
        Assert.Equal(TimerSurface.MiniTimer, MinimalModeRules.ForModeChange(MinimalMode.Always, TimerState.Stopped, miniTimerShowing: false));
        Assert.Equal(TimerSurface.MiniTimer, MinimalModeRules.ForModeChange(MinimalMode.OnStart, TimerState.Paused, miniTimerShowing: false));
        Assert.Null(MinimalModeRules.ForModeChange(MinimalMode.OnStart, TimerState.Stopped, miniTimerShowing: false));
        Assert.Equal(TimerSurface.MainWindow, MinimalModeRules.ForModeChange(MinimalMode.Off, TimerState.Running, miniTimerShowing: true));
        Assert.Null(MinimalModeRules.ForModeChange(MinimalMode.Off, TimerState.Running, miniTimerShowing: false));
    }

    [Fact]
    public void TheMiniTimerSaysWhatTheSessionIsFor()
    {
        using var test = new TestTimer();
        Assert.Equal("Focus", test.Timer.MiniTimerCaption);

        test.Timer.StartTimer();
        test.Timer.PauseTimer();
        Assert.Equal("Paused", test.Timer.MiniTimerCaption);

        test.Timer.CurrentTask = " Writing the report ";
        Assert.Equal("Writing the report", test.Timer.MiniTimerCaption);

        test.Timer.ResumeTimer();
        test.Advance(25 * 60);
        Assert.Equal("Time's up", test.Timer.MiniTimerCaption);

        test.Timer.CurrentTask = "";
        test.Timer.ResetTimer();
        Assert.Equal("Short break", test.Timer.MiniTimerCaption);
    }

    [Fact]
    public void TheSettingKeepsTheMacsValues()
    {
        using var test = new TestTimer();
        Assert.Equal(MinimalMode.Off, test.Settings.MinimalMode);

        test.Settings.MinimalMode = MinimalMode.OnStart;
        test.Settings.MiniTimerPos = new WindowPosition(3400, 40);
        var reloaded = new Settings(test.Settings.Folder);

        Assert.Equal(MinimalMode.OnStart, reloaded.MinimalMode);
        Assert.Equal(new WindowPosition(3400, 40), reloaded.MiniTimerPos);
        Assert.Contains("\"minimalMode\": \"start\"", File.ReadAllText(Path.Combine(test.Settings.Folder, "settings.json")));
    }
}
