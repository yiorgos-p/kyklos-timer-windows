namespace Kyklos.Tests;

public class TrayTests
{
    [Fact]
    public void StoppedShowsTheGlyph()
    {
        using var test = new TestTimer();

        Assert.Equal(TrayTone.Glyph, TrayIndicator.For(test.Timer).Tone);
    }

    [Fact]
    public void TheRingGoesGreenThenYellowThenRed()
    {
        using var test = new TestTimer();
        test.Timer.SetDuration(30);
        test.Timer.StartTimer();

        Assert.Equal((TrayTone.Green, 1.0), TrayIndicator.For(test.Timer));

        // A third left: 10 of 30 minutes.
        test.Advance(19 * 60 + 59);
        Assert.Equal(TrayTone.Green, TrayIndicator.For(test.Timer).Tone);
        test.Advance(1);
        Assert.Equal(TrayTone.Yellow, TrayIndicator.For(test.Timer).Tone);

        // A tenth left: 3 minutes.
        test.Advance(6 * 60 + 59);
        Assert.Equal(TrayTone.Yellow, TrayIndicator.For(test.Timer).Tone);
        test.Advance(1);
        Assert.Equal(TrayTone.Red, TrayIndicator.For(test.Timer).Tone);
        Assert.Equal(0.1, TrayIndicator.For(test.Timer).Fraction, 3);
    }

    [Fact]
    public void PausedIsGreyAndTimesUpIsAllRed()
    {
        using var test = new TestTimer();
        test.Timer.StartTimer();
        test.Advance(60);

        test.Timer.PauseTimer();
        Assert.Equal(TrayTone.Paused, TrayIndicator.For(test.Timer).Tone);

        test.Timer.ResumeTimer();
        test.Advance(24 * 60);
        Assert.Equal((TrayTone.Red, 1.0), TrayIndicator.For(test.Timer));
    }

    [Theory]
    [InlineData(1.0, 64)]
    [InlineData(0.5, 32)]
    [InlineData(1.0 / 3, 21)]
    [InlineData(0.004, 0)]
    public void TheIconChangesInSixtyFourSteps(double fraction, int step) =>
        Assert.Equal(step, TrayIndicator.Step(fraction));

    [Fact]
    public void TheTooltipHasTheTimeAndTaskUnderTheName()
    {
        using var test = new TestTimer();
        test.Timer.CurrentTask = "Writing the report";
        test.Timer.StartTimer();

        Assert.Equal("kyklos\n25:00 · Writing the report", TrayIndicator.Tooltip(test.Timer.TrayTitle));
    }

    [Fact]
    public void ATooltipTooLongForWindowsIsCutAtAWholeCharacter()
    {
        // A family emoji is one character but 11 UTF-16 units, so 24 of them fit the task's limit and not Windows'.
        const string family = "👨‍👩‍👧‍👦";
        using var test = new TestTimer();
        test.Timer.CurrentTask = string.Concat(Enumerable.Repeat(family, 30));
        test.Timer.StartTimer();

        var tooltip = TrayIndicator.Tooltip(test.Timer.TrayTitle);

        Assert.True(tooltip.Length <= TrayIndicator.TooltipLength);
        Assert.StartsWith("kyklos\n25:00 · " + family, tooltip);
        Assert.EndsWith(family + "…", tooltip);
    }

    [Theory]
    [InlineData("Short enough", 12, "Short enough")]
    [InlineData("Too long by far", 8, "Too lon…")]
    [InlineData("Café au lait", 5, "Caf…")] // the é is an e and an accent, kept together
    public void TextIsCutToFitAWindowsLimit(string text, int units, string fitted) =>
        Assert.Equal(fitted, Utf16Limit.Fit(text, units));
}
