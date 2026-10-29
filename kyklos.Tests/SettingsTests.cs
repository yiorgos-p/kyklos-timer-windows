using System.Text.Json.Nodes;

namespace Kyklos.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "kyklos-tests", Guid.NewGuid().ToString("N"));

    private string SettingsFile => Path.Combine(_folder, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void ValuesSurviveARestartUnderTheMacsNames()
    {
        var settings = new Settings(_folder);
        settings.StandardTimer = 45;
        settings.RepeatAlarm = false;
        settings.CurrentTask = "Γράφω την αναφορά";

        var reloaded = new Settings(_folder);

        Assert.Equal(45, reloaded.StandardTimer);
        Assert.False(reloaded.RepeatAlarm);
        Assert.Equal("Γράφω την αναφορά", reloaded.CurrentTask);

        var json = JsonNode.Parse(File.ReadAllText(SettingsFile))!.AsObject();
        Assert.Equal(new[] { "standardTimer", "repeatAlarm", "currentTask" }, json.Select(pair => pair.Key));
        Assert.False(File.Exists(SettingsFile + ".tmp"));
    }

    [Fact]
    public void AMissingFileReadsAsTheDefaults() => AssertDefaults(new Settings(_folder));

    [Fact]
    public void ABrokenFileReadsAsTheDefaults()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsFile, "{ not json");

        AssertDefaults(new Settings(_folder));
    }

    [Fact]
    public void OutOfRangeValuesAreClampedNeverRejected()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsFile, """{ "standardTimer": 90, "shortBreak": 0, "sessionsPerLongBreak": 12 }""");
        var settings = new Settings(_folder);

        Assert.Equal(60, settings.StandardTimer);
        Assert.Equal(1, settings.ShortBreak);
        Assert.Equal(8, settings.SessionsPerLongBreak);

        settings.ShortBreak = 45;
        settings.SessionsPerLongBreak = 1;

        Assert.Equal(30, settings.ShortBreak);
        Assert.Equal(2, settings.SessionsPerLongBreak);
    }

    [Fact]
    public void ReportsOnlyRealChanges()
    {
        var settings = new Settings(_folder);
        var changes = new List<string?>();
        settings.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        settings.RepeatAlarm = false;
        settings.RepeatAlarm = false;

        Assert.Equal(new string?[] { nameof(Settings.RepeatAlarm) }, changes);
    }

    private static void AssertDefaults(Settings settings)
    {
        Assert.Equal(25, settings.StandardTimer);
        Assert.Equal(5, settings.ShortBreak);
        Assert.Equal(10, settings.LongBreak);
        Assert.Equal(4, settings.SessionsPerLongBreak);
        Assert.True(settings.RepeatAlarm);
        Assert.Equal("Chime", settings.AlarmSound);
        Assert.Null(settings.CustomAlarmFile);
        Assert.Equal("", settings.CurrentTask);
        Assert.Equal(0, settings.SessionsToday);
    }
}
