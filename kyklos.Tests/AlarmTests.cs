namespace Kyklos.Tests;

public class AlarmTests
{
    [Fact]
    public void RingsEveryMinuteForTenMinutesThenStops()
    {
        using var test = new TestTimer();

        test.Finish();
        Assert.Single(test.Alarm.Plays);

        // Time's up, then +1:00 … +10:00.
        test.Advance(10 * 60);
        Assert.Equal(11, test.Alarm.Plays.Count);

        test.Advance(30 * 60);
        Assert.Equal(11, test.Alarm.Plays.Count);
    }

    [Fact]
    public void RingsOnceWithRepeatOff()
    {
        using var test = new TestTimer(settings => settings.RepeatAlarm = false);

        test.Finish();
        test.Advance(15 * 60);

        Assert.Single(test.Alarm.Plays);
    }

    [Fact]
    public void StaysQuietWhenASleepJumpsOvertimeAhead()
    {
        using var test = new TestTimer();
        test.Finish();
        test.Advance(30);

        test.Scheduler.Sleep(TimeSpan.FromMinutes(34.5));
        test.Timer.CatchUp();

        Assert.Equal("+35:00", test.Timer.DisplayTime);
        Assert.Single(test.Alarm.Plays);

        test.Advance(5 * 60);
        Assert.Single(test.Alarm.Plays);
    }

    [Fact]
    public void LeavingTimesUpEndsTheRepeats()
    {
        using var test = new TestTimer();
        test.Finish();

        test.Timer.ResetTimer();
        test.Advance(10 * 60);

        Assert.Single(test.Alarm.Plays);
    }

    [Fact]
    public void PlaysTheChosenSoundOrFallsBackToChime()
    {
        using var test = new TestTimer();
        var chime = AlarmSounds.BuiltInFile("Chime");

        test.Settings.AlarmSound = "Bowl";
        Assert.Equal(new[] { AlarmSounds.BuiltInFile("Bowl"), chime }, AlarmSounds.Candidates(test.Settings));

        // A name kyklos doesn't have, like the Mac's Classic, plays the default.
        test.Settings.AlarmSound = "Classic";
        Assert.Equal(new[] { chime }, AlarmSounds.Candidates(test.Settings));

        test.Settings.AlarmSound = AlarmSounds.Custom;
        Assert.Equal(new[] { chime }, AlarmSounds.Candidates(test.Settings));

        test.Settings.CustomAlarmFile = "custom-alarm.mp3";
        Assert.Equal(new[] { Path.Combine(test.Folder, "custom-alarm.mp3"), chime }, AlarmSounds.Candidates(test.Settings));
    }

    [Fact]
    public async Task APickedSoundIsCopiedSoItStillPlaysAfterTheOriginalIsDeleted()
    {
        using var test = new TestTimer();
        var original = WriteSound(test, "Song.MP3", seconds: 30);

        var message = await test.Timer.SetCustomAlarmAsync(original);

        Assert.Null(message);
        Assert.Equal(AlarmSounds.Custom, test.Settings.AlarmSound);
        Assert.Equal("Song.MP3", test.Settings.CustomAlarmName);
        Assert.Equal("custom-alarm.mp3", test.Settings.CustomAlarmFile);

        File.Delete(original);
        test.Timer.PlayAlarmSound();

        var copy = Path.Combine(test.Folder, "custom-alarm.mp3");
        Assert.True(File.Exists(copy));
        Assert.Equal(copy, test.Alarm.Plays[^1][0]);
    }

    [Fact]
    public async Task APickedSoundReplacesTheOldCopy()
    {
        using var test = new TestTimer();
        await test.Timer.SetCustomAlarmAsync(WriteSound(test, "old.wav", seconds: 3));

        await test.Timer.SetCustomAlarmAsync(WriteSound(test, "new.m4a", seconds: 3));

        Assert.False(File.Exists(Path.Combine(test.Folder, "custom-alarm.wav")));
        Assert.True(File.Exists(Path.Combine(test.Folder, "custom-alarm.m4a")));
        Assert.Equal("new.m4a", test.Settings.CustomAlarmName);
    }

    [Fact]
    public async Task ASoundOver20MegabytesIsRefused()
    {
        using var test = new TestTimer();
        var file = WriteSound(test, "big.wav", seconds: 5);
        using (var stream = File.OpenWrite(file))
        {
            stream.SetLength(AlarmSounds.MaximumBytes + 1);
        }

        Assert.Equal(AlarmSounds.TooLargeMessage, await test.Timer.SetCustomAlarmAsync(file));
        Assert.Equal(AlarmSounds.Default, test.Settings.AlarmSound);
    }

    [Theory]
    [InlineData(null, AlarmSounds.UnreadableMessage)]
    [InlineData(0.0, AlarmSounds.UnreadableMessage)]
    [InlineData(30.5, AlarmSounds.TooLongMessage)]
    public async Task AnUnplayableOrLongSoundIsRefused(double? seconds, string message)
    {
        using var test = new TestTimer();
        var file = WriteSound(test, "sound.wav", seconds);

        Assert.Equal(message, await test.Timer.SetCustomAlarmAsync(file));
        Assert.Equal(AlarmSounds.Default, test.Settings.AlarmSound);
        Assert.Null(test.Settings.CustomAlarmFile);
    }

    [Fact]
    public async Task AMissingFileCantBePlayed()
    {
        using var test = new TestTimer();

        var message = await test.Timer.SetCustomAlarmAsync(Path.Combine(test.Folder, "gone.wav"));

        Assert.Equal(AlarmSounds.UnreadableMessage, message);
    }

    [Fact]
    public async Task AFileWithNoPathCantBePlayed()
    {
        using var test = new TestTimer();

        // What the file picker gives for a sound on a phone, which isn't in any folder.
        var message = await test.Timer.SetCustomAlarmAsync("");

        Assert.Equal(AlarmSounds.UnreadableMessage, message);
        Assert.Null(test.Settings.CustomAlarmFile);
    }

    /// <summary>Writes a stand-in sound file that the fake player says lasts this long (or can't play, for null).</summary>
    private static string WriteSound(TestTimer test, string name, double? seconds)
    {
        var file = Path.Combine(test.Folder, "picked", name);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, [1, 2, 3]);

        if (seconds is { } length)
        {
            test.Alarm.Lengths[file] = TimeSpan.FromSeconds(length);
        }

        return file;
    }
}
