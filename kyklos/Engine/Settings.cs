using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Kyklos.Engine;

/// <summary>
/// kyklos's settings, kept under the Mac app's UserDefaults names in settings.json. As with UserDefaults, a value that's
/// missing or unreadable reads as its default, so a file from an older version keeps working.
/// </summary>
public sealed class Settings : INotifyPropertyChanged
{
    public const int MinimumSessionsPerLongBreak = 2;
    public const int MaximumSessionsPerLongBreak = 8;

    private const string FileName = "settings.json";

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        // Keep a task typed in Greek or any other script readable in the file rather than as \u escapes.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>How many times a busy settings file is tried, a tenth of a second apart, before it counts as unreadable.</summary>
    private const int ReadAttempts = 20;

    private readonly JsonObject _values;

    public Settings(string folder)
    {
        Folder = folder;
        _values = Read(Path.Combine(folder, FileName));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>%LOCALAPPDATA%\kyklos, where kyklos keeps its settings.</summary>
    public static string DefaultFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "kyklos");

    /// <summary>The folder the settings are saved in. A custom alarm sound is copied in next to them.</summary>
    public string Folder { get; }

    public int StandardTimer
    {
        get => GetMinutes("standardTimer", TimerPhase.Focus);
        set => SetMinutes("standardTimer", TimerPhase.Focus, value);
    }

    public int ShortBreak
    {
        get => GetMinutes("shortBreak", TimerPhase.ShortBreak);
        set => SetMinutes("shortBreak", TimerPhase.ShortBreak, value);
    }

    public int LongBreak
    {
        get => GetMinutes("longBreak", TimerPhase.LongBreak);
        set => SetMinutes("longBreak", TimerPhase.LongBreak, value);
    }

    /// <summary>A long break comes after every this many focus sessions, shown as the dots under the dial.</summary>
    public int SessionsPerLongBreak
    {
        get => Math.Clamp(GetInt("sessionsPerLongBreak") ?? 4, MinimumSessionsPerLongBreak, MaximumSessionsPerLongBreak);
        set => Set("sessionsPerLongBreak", Math.Clamp(value, MinimumSessionsPerLongBreak, MaximumSessionsPerLongBreak));
    }

    /// <summary>Focus sessions finished on <see cref="SessionsDay"/>.</summary>
    public int SessionsToday
    {
        get => Math.Max(0, GetInt("sessionsToday") ?? 0);
        set => Set("sessionsToday", value);
    }

    /// <summary>The day <see cref="SessionsToday"/> counts, like "2026-10-29".</summary>
    public string SessionsDay
    {
        get => GetString("sessionsDay") ?? "";
        set => Set("sessionsDay", value);
    }

    public bool RepeatAlarm
    {
        get => GetBool("repeatAlarm") ?? true;
        set => Set("repeatAlarm", value);
    }

    /// <summary>A built-in sound's name, or <see cref="AlarmSounds.Custom"/> for a file the user picked.</summary>
    public string AlarmSound
    {
        get => GetString("alarmSound") ?? AlarmSounds.Default;
        set => Set("alarmSound", value);
    }

    /// <summary>The picked sound's original file name, shown in Settings.</summary>
    public string? CustomAlarmName
    {
        get => GetString("customAlarmName");
        set => Set("customAlarmName", value);
    }

    /// <summary>The picked sound's copy in <see cref="Folder"/>.</summary>
    public string? CustomAlarmFile
    {
        get => GetString("customAlarmFile");
        set => Set("customAlarmFile", value);
    }

    /// <summary>"Controls: Mouse": click the dial to start or pause, triple-click it to reset.</summary>
    public bool UseMouseControls
    {
        get => GetBool("useMouseControls") ?? false;
        set => Set("useMouseControls", value);
    }

    /// <summary>Hides the phase switcher while the timer runs.</summary>
    public bool ZenMode
    {
        get => GetBool("zenMode") ?? false;
        set => Set("zenMode", value);
    }

    public bool KeepWindowOnTop
    {
        get => GetBool("keepWindowOnTop") ?? false;
        set => Set("keepWindowOnTop", value);
    }

    public MinimalMode MinimalMode
    {
        get => GetString("minimalMode") switch
        {
            "start" => MinimalMode.OnStart,
            "always" => MinimalMode.Always,
            _ => MinimalMode.Off,
        };
        set => Set("minimalMode", value switch
        {
            MinimalMode.OnStart => "start",
            MinimalMode.Always => "always",
            _ => "off",
        });
    }

    /// <summary>The Mac's "Show in Dock". When off, kyklos lives in the tray and its window.</summary>
    public bool ShowInTaskbar
    {
        get => GetBool("showInTaskbar") ?? true;
        set => Set("showInTaskbar", value);
    }

    public AppTheme Appearance
    {
        get => GetString("appearance") switch
        {
            "light" => AppTheme.Light,
            "dark" => AppTheme.Dark,
            _ => AppTheme.System,
        };
        set => Set("appearance", value switch
        {
            AppTheme.Light => "light",
            AppTheme.Dark => "dark",
            _ => "system",
        });
    }

    public bool GlobalShortcutEnabled
    {
        get => GetBool("globalShortcutEnabled") ?? true;
        set => Set("globalShortcutEnabled", value);
    }

    /// <summary>The global shortcut, kept as a virtual-key code and RegisterHotKey's modifier flags.</summary>
    public GlobalShortcut GlobalShortcut
    {
        get => GetInt("globalShortcutKeyCode") is { } key && GetInt("globalShortcutModifiers") is { } modifiers
            && new GlobalShortcut(key, (ShortcutModifiers)modifiers) is { IsValid: true } shortcut
                ? shortcut
                : GlobalShortcut.Default;

        // Both at once, so nothing ever sees the new key with the old modifiers.
        set => Set(
        [
            ("globalShortcutKeyCode", value.Key),
            ("globalShortcutModifiers", (int)value.Modifiers),
        ]);
    }

    public string CurrentTask
    {
        get => GetString("currentTask") ?? "";
        set => Set("currentTask", value);
    }

    /// <summary>Where the main window was left, in screen pixels.</summary>
    public WindowPosition? MainWindowPos
    {
        get => GetPosition("mainWindowPos");
        set => SetPosition("mainWindowPos", value);
    }

    /// <summary>Where the mini timer was left, in screen pixels.</summary>
    public WindowPosition? MiniTimerPos
    {
        get => GetPosition("miniTimerPos");
        set => SetPosition("miniTimerPos", value);
    }

    /// <summary>The preset length of a phase.</summary>
    public int Minutes(TimerPhase phase) => phase switch
    {
        TimerPhase.ShortBreak => ShortBreak,
        TimerPhase.LongBreak => LongBreak,
        _ => StandardTimer,
    };

    private int GetMinutes(string key, TimerPhase phase) =>
        Math.Clamp(GetInt(key) ?? phase.DefaultMinutes, 1, phase.MaximumMinutes);

    private void SetMinutes(string key, TimerPhase phase, int minutes, [CallerMemberName] string property = "") =>
        Set(key, Math.Clamp(minutes, 1, phase.MaximumMinutes), property);

    private int? GetInt(string key) =>
        _values[key] is JsonValue value && value.TryGetValue(out int number) ? number : null;

    private bool? GetBool(string key) =>
        _values[key] is JsonValue value && value.TryGetValue(out bool flag) ? flag : null;

    private string? GetString(string key) =>
        _values[key] is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    private WindowPosition? GetPosition(string key) =>
        _values[key] is JsonObject point
        && point["x"] is JsonValue x && x.TryGetValue(out int left)
        && point["y"] is JsonValue y && y.TryGetValue(out int top)
            ? new WindowPosition(left, top)
            : null;

    private void SetPosition(string key, WindowPosition? position, [CallerMemberName] string property = "") =>
        Set(key, position is { } point ? new JsonObject { ["x"] = point.X, ["y"] = point.Y } : null, property);

    private void Set(string key, JsonNode? value, [CallerMemberName] string property = "") => Set([(key, value)], property);

    /// <summary>Writes these values, saves and reports once, and only if something really changed.</summary>
    private void Set((string Key, JsonNode? Value)[] values, [CallerMemberName] string property = "")
    {
        var changed = false;

        foreach (var (key, value) in values)
        {
            if (!JsonNode.DeepEquals(_values[key], value))
            {
                _values[key] = value;
                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        Save();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }

    private static JsonObject Read(string file)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return JsonNode.Parse(File.ReadAllText(file)) as JsonObject ?? new JsonObject();
            }
            catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
            {
                // No file yet: the first launch.
                return new JsonObject();
            }
            catch (IOException) when (attempt < ReadAttempts)
            {
                // Another program (a backup, a virus scan) has it open for a moment, as can happen at sign-in. Starting
                // from the defaults would write them over the real settings at the first change, so wait and try again.
                Thread.Sleep(100);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                // One that can't be read even so, or isn't settings at all: start from the defaults.
                return new JsonObject();
            }
        }
    }

    /// <summary>Writes the file in one rename, so a crash or power cut mid-write can never leave half a file.</summary>
    private void Save()
    {
        var file = Path.Combine(Folder, FileName);
        var temporary = file + ".tmp";

        try
        {
            Directory.CreateDirectory(Folder);

            using (var stream = File.Create(temporary))
            {
                using (var writer = new Utf8JsonWriter(stream, WriterOptions))
                {
                    _values.WriteTo(writer);
                }

                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, file, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A timer shouldn't stop working because a settings file can't be written. The values stay in memory,
            // and the next change tries again.
        }
    }
}
