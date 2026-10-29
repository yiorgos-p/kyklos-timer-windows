namespace Kyklos.Tests;

public class ShortcutTests
{
    private const ShortcutModifiers Ctrl = ShortcutModifiers.Control;
    private const ShortcutModifiers Alt = ShortcutModifiers.Alt;
    private const ShortcutModifiers Shift = ShortcutModifiers.Shift;
    private const ShortcutModifiers Win = ShortcutModifiers.Windows;

    /// <summary>A layout where the key types its letter in lower case, as a real one does.</summary>
    private static string Lower(int key) => ((char)key).ToString().ToLowerInvariant();

    [Fact]
    public void TheDefaultIsCtrlAltK()
    {
        Assert.Equal("Ctrl+Alt+K", GlobalShortcut.Default.Describe(Lower));
        Assert.True(GlobalShortcut.Default.IsValid);
    }

    [Fact]
    public void ModifiersAreWrittenInWindowsOrder() =>
        Assert.Equal("Win+Ctrl+Alt+Shift+K", new GlobalShortcut('K', Shift | Alt | Ctrl | Win).Describe(Lower));

    [Theory]
    [InlineData(Ctrl | Alt, true)]
    [InlineData(Win | Shift, true)]
    [InlineData(Ctrl | Shift, true)]
    [InlineData(Ctrl, false)]
    [InlineData(Alt | Shift, false)]
    [InlineData(ShortcutModifiers.None, false)]
    public void ItNeedsCtrlOrWinAndOneMoreModifier(ShortcutModifiers modifiers, bool valid) =>
        Assert.Equal(valid, new GlobalShortcut('K', modifiers).IsValid);

    [Theory]
    [InlineData(0x20, "Ctrl+Alt+Space")]
    [InlineData(0x0D, "Ctrl+Alt+Enter")]
    [InlineData(0x22, "Ctrl+Alt+Page Down")]
    [InlineData(0x25, "Ctrl+Alt+Left")]
    [InlineData(0x74, "Ctrl+Alt+F5")]
    [InlineData(0x7B, "Ctrl+Alt+F12")]
    public void KeysThatTypeNothingHaveNames(int key, string text) =>
        Assert.Equal(text, new GlobalShortcut(key, Ctrl | Alt).Describe(_ => throw new InvalidOperationException()));

    [Fact]
    public void OtherKeysShowWhatTheyTypeOnTheLayoutInCapitals() =>
        Assert.Equal("Ctrl+Alt+Ö", new GlobalShortcut(0xC0, Ctrl | Alt).Describe(_ => "ö"));

    [Fact]
    public void TheShortcutIsSavedAndAnUnusableOneReadsAsTheDefault()
    {
        using var test = new TestTimer();
        var changes = new List<string?>();
        test.Settings.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        test.Settings.GlobalShortcut = new GlobalShortcut('P', Win | Shift);
        Assert.Equal([nameof(Settings.GlobalShortcut)], changes);
        Assert.Equal(new GlobalShortcut('P', Win | Shift), new Settings(test.Settings.Folder).GlobalShortcut);

        test.Settings.GlobalShortcut = new GlobalShortcut('P', Shift);
        Assert.Equal(GlobalShortcut.Default, new Settings(test.Settings.Folder).GlobalShortcut);
    }

    [Fact]
    public void TheAlarmSaysWhileItsPlaying()
    {
        using var test = new TestTimer();
        Assert.False(test.Timer.IsAlarmPlaying);

        test.Timer.PlayAlarmSound();
        Assert.True(test.Timer.IsAlarmPlaying);

        test.Alarm.End();
        Assert.False(test.Timer.IsAlarmPlaying);

        test.Timer.PlayAlarmSound();
        test.Timer.StopAlarmSound();
        Assert.False(test.Timer.IsAlarmPlaying);
    }
}
