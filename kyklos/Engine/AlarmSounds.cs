namespace Kyklos.Engine;

/// <summary>The alarm: one of the sounds that come with kyklos, or any short audio file the user picks.</summary>
public static class AlarmSounds
{
    /// <summary>
    /// The built-in sounds, in the order Settings lists them. They're kyklos's own, synthesized from sine waves, so
    /// nobody else holds rights to them.
    /// </summary>
    public static readonly IReadOnlyList<string> BuiltIn =
        ["Chime", "Pop", "Digital", "Felt", "Bowl", "Low Bowl", "Marimba", "Beacon"];

    public const string Default = "Chime";

    /// <summary>The "alarmSound" setting for a file the user picked, rather than a built-in name.</summary>
    public const string Custom = "custom";

    // Limits for a picked sound: it rings again every minute in overtime, so it has to be short.
    public static readonly TimeSpan MaximumLength = TimeSpan.FromSeconds(30);
    public const long MaximumBytes = 20 * 1024 * 1024;

    public const string TooLargeMessage = "That file is over 20 MB. Pick a smaller one.";
    public const string UnreadableMessage = "That file can't be played. Try an MP3, M4A or WAV file.";
    public const string TooLongMessage = "That sound is longer than 30 seconds. Pick a shorter one.";
    public const string NoPickerMessage = "Windows couldn't open the file picker. Try again.";

    /// <summary>A built-in sound's file, in the Sounds folder next to kyklos.exe.</summary>
    public static string BuiltInFile(string name) => Path.Combine(AppContext.BaseDirectory, "Sounds", name + ".wav");

    /// <summary>The files to try for the alarm: the chosen sound if it still opens, otherwise the default one.</summary>
    public static IReadOnlyList<string> Candidates(Settings settings)
    {
        var chosen = settings.AlarmSound;
        string? file = null;

        if (chosen == Custom && settings.CustomAlarmFile is { } copy)
        {
            file = Path.Combine(settings.Folder, copy);
        }
        else if (BuiltIn.Contains(chosen))
        {
            file = BuiltInFile(chosen);
        }

        return file is null ? [BuiltInFile(Default)] : [file, BuiltInFile(Default)];
    }
}
