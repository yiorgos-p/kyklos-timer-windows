namespace Kyklos.Tests;

public class TextTests
{
    private const string Report = "Writing the report";

    [Fact]
    public void FocusDoneWithATask()
    {
        using var test = new TestTimer();
        test.Timer.CurrentTask = Report;

        test.Finish();

        Assert.Equal(("Focus done: Writing the report", "Time for a short break."), test.Notifier.Posted.Single());
    }

    [Fact]
    public void FocusDoneWithoutATask()
    {
        using var test = new TestTimer();

        test.Finish();

        Assert.Equal(("Focus done", "Time for a short break."), test.Notifier.Posted.Single());
    }

    [Fact]
    public void TheLastFocusOfARoundSuggestsALongBreak()
    {
        using var test = new TestTimer();

        for (var session = 1; session < 4; session++)
        {
            test.Finish();
            test.Timer.SetPhase(TimerPhase.Focus);
        }

        test.Finish();

        Assert.Equal("Time for a long break.", test.Notifier.Posted[^1].Body);
    }

    [Fact]
    public void BreaksOverWithATask()
    {
        using var test = new TestTimer();
        test.Timer.CurrentTask = Report;
        test.Timer.SetPhase(TimerPhase.ShortBreak);

        test.Finish();

        Assert.Equal(("Break's over", "Back to: Writing the report"), test.Notifier.Posted.Single());
    }

    [Fact]
    public void BreaksOverWithoutATask()
    {
        using var test = new TestTimer();
        test.Timer.SetPhase(TimerPhase.LongBreak);

        test.Finish();

        Assert.Equal(("Break's over", "Ready to focus again?"), test.Notifier.Posted.Single());
    }

    [Fact]
    public void TheButtonsFollowTheState()
    {
        using var test = new TestTimer();
        Assert.Equal(("Start", "Reset"), (test.Timer.PrimaryActionTitle, test.Timer.ResetActionTitle));

        test.Timer.StartTimer();
        Assert.Equal("Pause", test.Timer.PrimaryActionTitle);

        test.Timer.PauseTimer();
        Assert.Equal("Resume", test.Timer.PrimaryActionTitle);

        test.Timer.ResumeTimer();
        test.Advance(25 * 60);
        Assert.Equal(("Start short break", "Stop"), (test.Timer.PrimaryActionTitle, test.Timer.ResetActionTitle));

        test.Timer.ToggleTimer();
        test.Advance(5 * 60);
        Assert.Equal("Start focus", test.Timer.PrimaryActionTitle);
    }

    [Fact]
    public void TheDotsExplainTheRhythm()
    {
        Assert.Equal("1 focus session finished today. A long break comes after every 4.", SessionDots.Description(1, 4));
        Assert.Equal("3 focus sessions finished today. A long break comes after every 4.", SessionDots.Description(3, 4));
    }

    [Fact]
    public void TheTrayShowsOnlyTheTimeWhileStopped()
    {
        using var test = new TestTimer();
        test.Timer.CurrentTask = Report;

        Assert.Equal("25:00", test.Timer.TrayTitle);
    }

    [Fact]
    public void TheTrayShowsTheTaskWhileASessionIsOn()
    {
        using var test = new TestTimer();
        test.Timer.CurrentTask = Report;
        test.Timer.StartTimer();
        test.Advance(47);

        Assert.Equal("24:13 · Writing the report", test.Timer.TrayTitle);

        test.Timer.PauseTimer();
        Assert.Equal("Paused 24:13 · Writing the report", test.Timer.TrayTitle);
    }

    [Fact]
    public void TheTrayShowsOvertime()
    {
        using var test = new TestTimer();
        test.Timer.CurrentTask = Report;
        test.Finish();

        test.Advance(130);

        Assert.Equal("+02:10 · Writing the report", test.Timer.TrayTitle);
    }

    [Theory]
    [InlineData("Twenty-four characters!!", "Twenty-four characters!!")]
    [InlineData("Twenty-five characters!!!", "Twenty-five characters!…")]
    [InlineData("   ", null)]
    public void TheTrayShortensLongTasksAndSkipsBlankOnes(string task, string? shown)
    {
        using var test = new TestTimer();
        test.Timer.CurrentTask = task;

        test.Timer.StartTimer();

        Assert.Equal(shown is null ? "25:00" : "25:00 · " + shown, test.Timer.TrayTitle);
    }

    [Fact]
    public void TheTrayCountsCharactersAsTheyLook()
    {
        using var test = new TestTimer();
        test.Timer.CurrentTask = string.Concat(Enumerable.Repeat("🍅", 24));

        test.Timer.StartTimer();

        Assert.Equal("25:00 · " + test.Timer.CurrentTask, test.Timer.TrayTitle);
    }

    [Fact]
    public void TheTaskIsSavedHalfASecondAfterTypingPausesAndOnQuit()
    {
        using var test = new TestTimer();

        test.Timer.CurrentTask = "W";
        test.Advance(0.3);
        test.Timer.CurrentTask = "Wr";
        test.Advance(0.4);
        Assert.Equal("", test.Settings.CurrentTask);

        test.Advance(0.1);
        Assert.Equal("Wr", test.Settings.CurrentTask);

        test.Timer.CurrentTask = "Write";
        test.Timer.SaveTask();
        Assert.Equal("Write", test.Settings.CurrentTask);
        Assert.Equal(0, test.Scheduler.Pending);

        var restarted = new TimerViewModel(test.Settings, test.Clock, test.Scheduler, test.Alarm, test.Notifier);
        Assert.Equal("Write", restarted.CurrentTask);
    }
}
