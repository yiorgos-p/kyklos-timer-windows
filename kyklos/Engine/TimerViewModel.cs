using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Kyklos.Engine;

/// <summary>
/// The timer: phases, time keeping, overtime and the alarm, today's sessions and the task. The window, the tray and the
/// mini timer all show this one object.
/// </summary>
public sealed class TimerViewModel : INotifyPropertyChanged
{
    /// <summary>The longest session the dial allows.</summary>
    public const int MaximumMinutes = 60;

    /// <summary>How long the alarm keeps ringing in overtime, so it doesn't go on all day if nobody is at the PC.</summary>
    private const int AlarmRepeatMinutes = 10;

    // A millisecond of slack when rounding, as on the Mac: the alarm goes off the instant the display would read 00:00,
    // and a tick that fires a hair early still shows the new second rather than the old one again.
    private static readonly TimeSpan Slack = TimeSpan.FromMilliseconds(1);

    private readonly Settings _settings;
    private readonly IClock _clock;
    private readonly IScheduler _scheduler;
    private readonly IAlarmPlayer _alarm;
    private readonly INotifier _notifier;

    // Time left is always measured against the monotonic clock, which keeps counting while the PC sleeps and ignores
    // changes to the system clock. The tick only decides when to redraw, never how much time has passed.
    private TimeSpan? _endInstant;
    private TimeSpan _pausedRemaining;
    private IDisposable? _tick;
    private IDisposable? _pendingTaskSave;
    private string _currentTask;

    /// <summary>The minute of overtime the alarm last rang for, so each minute rings at most once.</summary>
    private int _lastAlarmMinute;

    public TimerViewModel(Settings settings, IClock clock, IScheduler scheduler, IAlarmPlayer alarm, INotifier notifier)
    {
        _settings = settings;
        _clock = clock;
        _scheduler = scheduler;
        _alarm = alarm;
        _notifier = notifier;
        _currentTask = settings.CurrentTask;

        // Start with the saved Focus preset.
        TimerDuration = settings.Minutes(TimerPhase.Focus);
        UpdateRemainingTime();
        LoadTodaysSessions();

        settings.PropertyChanged += OnSettingChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Whole seconds left, rounded up: a fresh 25-minute timer reads 25:00, and the alarm goes off the instant it would
    /// read 00:00.
    /// </summary>
    public int RemainingTime
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                Notify(nameof(DisplayTime), nameof(DialFraction), nameof(Progress), nameof(StatusCaption), nameof(TrayTitle));
            }
        }
    }

    /// <summary>The length of the next run in minutes, which the dial and typing change while stopped.</summary>
    public int TimerDuration { get; private set => Set(ref field, value); }

    public TimerState State
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                Notify(nameof(DisplayTime), nameof(DialFraction), nameof(StatusCaption), nameof(NextPhase), nameof(TrayTitle));
            }
        }
    }

    public TimerPhase Phase
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                Notify(nameof(NextPhase));
            }
        }
    }

    /// <summary>Whole seconds since the timer finished, while it waits for the next phase to be started.</summary>
    public int Overtime
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                Notify(nameof(DisplayTime), nameof(TrayTitle));
            }
        }
    }

    /// <summary>The length of the current run in seconds, so views can show how much of it is left.</summary>
    public int SessionLength
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                Notify(nameof(Progress));
            }
        }
    }

    public int CompletedFocusToday
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                Notify(nameof(NextPhase));
            }
        }
    }

    /// <summary>What the session is for. It's saved half a second after typing pauses, and again on quit.</summary>
    public string CurrentTask
    {
        get => _currentTask;
        set
        {
            if (Set(ref _currentTask, value))
            {
                Notify(nameof(TrayTitle));
                ScheduleTaskSave();
            }
        }
    }

    // Derived state

    /// <summary>Fraction of the current run still left, from 1 down to 0.</summary>
    public double Progress => SessionLength > 0 ? Math.Min(1, (double)RemainingTime / SessionLength) : 0;

    /// <summary>
    /// How much of the 60-minute dial the time left fills, so 25 minutes always looks like 25 minutes and a 5-minute
    /// break looks short. Full once time is up, so the whole ring turns orange.
    /// </summary>
    public double DialFraction =>
        State == TimerState.Finished ? 1 : Math.Min(1, RemainingTime / (MaximumMinutes * 60.0));

    /// <summary>When the running timer will reach zero, on the wall clock.</summary>
    public DateTime? EndDate =>
        State == TimerState.Running && _endInstant is { } end ? _clock.LocalNow + (end - _clock.Now) : null;

    /// <summary>The phase that follows this one: a break after focus (a long one every few sessions), focus after a break.</summary>
    public TimerPhase NextPhase
    {
        get
        {
            if (Phase != TimerPhase.Focus)
            {
                return TimerPhase.Focus;
            }

            // Once a focus session has finished it's already counted, so look at the count as it will be after this one.
            // (It's 0 only if the count was reset in the meantime, which isn't a full round.)
            var sessions = CompletedFocusToday + (State == TimerState.Finished ? 0 : 1);
            return sessions > 0 && sessions % _settings.SessionsPerLongBreak == 0
                ? TimerPhase.LongBreak
                : TimerPhase.ShortBreak;
        }
    }

    /// <summary>The main time readout: the countdown, or "+mm:ss" of overtime once the session has finished.</summary>
    public string DisplayTime => State == TimerState.Finished ? "+" + TimeString(Overtime) : TimeString(RemainingTime);

    /// <summary>The line under the time.</summary>
    public string StatusCaption => State switch
    {
        TimerState.Running => EndDate is { } end ? "ends " + end.ToString("t", CultureInfo.CurrentCulture) : "",
        TimerState.Paused => "paused",
        TimerState.Finished => "time's up",
        _ => "ready",
    };

    /// <summary>
    /// The time and, while a session is on, what it's for, so a glance at the tray is a reminder. The Mac shows this in
    /// the menu bar; on Windows it's the tray icon's tooltip.
    /// </summary>
    public string TrayTitle
    {
        get
        {
            var title = State == TimerState.Paused ? "Paused " + DisplayTime : DisplayTime;
            var task = CurrentTask.Trim();

            if (State != TimerState.Stopped && task.Length > 0)
            {
                title += " · " + Shorten(task, 24);
            }

            return title;
        }
    }

    // Controls

    public void ToggleTimer()
    {
        switch (State)
        {
            case TimerState.Running:
                PauseTimer();
                break;
            case TimerState.Paused:
                ResumeTimer();
                break;
            case TimerState.Stopped:
                StartTimer();
                break;
            case TimerState.Finished:
                StartNextPhase();
                break;
        }
    }

    public void StartTimer()
    {
        SessionLength = TimerDuration * 60;
        Run();
    }

    public void PauseTimer()
    {
        if (State != TimerState.Running || _endInstant is not { } end)
        {
            return;
        }

        var left = end - _clock.Now;
        _pausedRemaining = left > TimeSpan.Zero ? left : TimeSpan.Zero;
        StopTicking();
        State = TimerState.Paused;
        RemainingTime = WholeSeconds(_pausedRemaining);
    }

    public void ResumeTimer() => Run();

    /// <summary>Stops the timer. After a finished session this moves on to the next phase without starting it.</summary>
    public void ResetTimer()
    {
        if (State == TimerState.Finished)
        {
            SetPhase(NextPhase);
            return;
        }

        StopTicking();
        State = TimerState.Stopped;
        UpdateRemainingTime();
    }

    public void SetPhase(TimerPhase phase)
    {
        // A longer custom alarm shouldn't keep ringing into the next session.
        StopAlarmSound();
        StopTicking();
        State = TimerState.Stopped;
        Phase = phase;
        TimerDuration = _settings.Minutes(phase);
        UpdateRemainingTime();
    }

    /// <summary>Dragging the dial or typing in it: sets the length of a timer that hasn't started yet.</summary>
    public void SetDuration(int minutes)
    {
        minutes = Math.Clamp(minutes, 1, MaximumMinutes);

        if (State != TimerState.Stopped || minutes == TimerDuration)
        {
            return;
        }

        TimerDuration = minutes;
        UpdateRemainingTime();
    }

    public void StartNextPhase()
    {
        SetPhase(NextPhase);
        StartTimer();
    }

    /// <summary>Timers don't fire while the PC sleeps, so the app calls this as soon as it wakes or the clock changes.</summary>
    public void CatchUp() => Tick();

    private void UpdateRemainingTime()
    {
        Overtime = 0;
        _pausedRemaining = TimeSpan.FromMinutes(TimerDuration);
        RemainingTime = WholeSeconds(_pausedRemaining);
        SessionLength = RemainingTime;
    }

    private void OnSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        var stopped = State == TimerState.Stopped;

        switch (e.PropertyName)
        {
            // A preset changed in Settings: show its new length now, unless the timer is in use.
            case nameof(Settings.StandardTimer) when stopped && Phase == TimerPhase.Focus:
            case nameof(Settings.ShortBreak) when stopped && Phase == TimerPhase.ShortBreak:
            case nameof(Settings.LongBreak) when stopped && Phase == TimerPhase.LongBreak:
                SetPhase(Phase);
                break;

            case nameof(Settings.SessionsPerLongBreak):
                Notify(nameof(NextPhase));
                break;
        }
    }

    // Ticking

    private void Run()
    {
        _endInstant = _clock.Now + _pausedRemaining;
        State = TimerState.Running;
        Tick();
    }

    private void Tick()
    {
        _tick?.Dispose();
        _tick = null;

        if (_endInstant is not { } end)
        {
            return;
        }

        TimeSpan untilNextChange;

        switch (State)
        {
            case TimerState.Running:
                var left = end - _clock.Now;

                if (left <= Slack)
                {
                    Finish();
                    return;
                }

                var shown = WholeSeconds(left);
                RemainingTime = shown;

                // Fire exactly when the display should change to the next second, rather than on a drifting 1-second repeat.
                untilNextChange = left - TimeSpan.FromSeconds(shown - 1);
                break;

            case TimerState.Finished:
                var over = _clock.Now - end;
                var shownOver = WholeSecondsOver(over);

                if (Overtime != shownOver)
                {
                    Overtime = shownOver;
                    RepeatAlarmIfDue();
                }

                untilNextChange = TimeSpan.FromSeconds(shownOver + 1) - over;
                break;

            default:
                return;
        }

        _tick = _scheduler.Schedule(untilNextChange, Tick);
    }

    private void StopTicking()
    {
        _tick?.Dispose();
        _tick = null;
        _endInstant = null;
    }

    private void Finish()
    {
        // Keep the end instant, so overtime counts from the exact moment the timer ran out. Leaving overtime ends it.
        RemainingTime = 0;
        Overtime = 0;
        _lastAlarmMinute = 0;
        State = TimerState.Finished;

        if (Phase == TimerPhase.Focus)
        {
            RecordFocusSession();
        }

        PlayAlarmSound();
        PostFinishedNotification();
        Tick();
    }

    /// <summary>Seconds left as shown: rounded up, so a fresh 25 minutes reads 25:00 rather than 24:59.</summary>
    private static int WholeSeconds(TimeSpan left)
    {
        var ticks = (left - Slack).Ticks;
        return ticks <= 0 ? 0 : (int)((ticks + TimeSpan.TicksPerSecond - 1) / TimeSpan.TicksPerSecond);
    }

    /// <summary>Seconds of overtime as shown: rounded down, so it reads +00:00 for the whole first second.</summary>
    private static int WholeSecondsOver(TimeSpan over)
    {
        var ticks = (over + Slack).Ticks;
        return ticks <= 0 ? 0 : (int)(ticks / TimeSpan.TicksPerSecond);
    }

    // Today's sessions

    private string TodayKey
    {
        get
        {
            var today = _clock.LocalNow;
            return string.Create(CultureInfo.InvariantCulture, $"{today.Year}-{today.Month}-{today.Day}");
        }
    }

    /// <summary>Reads today's count, which is 0 once the saved day isn't today. The app calls this at midnight and on wake.</summary>
    public void LoadTodaysSessions() =>
        CompletedFocusToday = _settings.SessionsDay == TodayKey ? _settings.SessionsToday : 0;

    public void ResetTodaysSessions() => SaveTodaysSessions(0);

    private void RecordFocusSession()
    {
        // The day may have changed since the count was read.
        LoadTodaysSessions();
        SaveTodaysSessions(CompletedFocusToday + 1);
    }

    private void SaveTodaysSessions(int count)
    {
        CompletedFocusToday = count;
        _settings.SessionsDay = TodayKey;
        _settings.SessionsToday = count;
    }

    // Alarm

    public void PlayAlarmSound() => _alarm.Play(AlarmSounds.Candidates(_settings));

    public void StopAlarmSound() => _alarm.Stop();

    /// <summary>Rings again at each full minute of overtime, for the first few minutes, unless turned off in Settings.</summary>
    private void RepeatAlarmIfDue()
    {
        var minute = Overtime / 60;

        if (minute <= _lastAlarmMinute)
        {
            return;
        }

        _lastAlarmMinute = minute;

        // After a long sleep the overtime jumps far past the limit; stay quiet then rather than ring once on wake.
        if (_settings.RepeatAlarm && minute <= AlarmRepeatMinutes)
        {
            PlayAlarmSound();
        }
    }

    /// <summary>
    /// Checks a sound picked in Settings against the limits, then makes it the alarm. It's copied into kyklos's own
    /// folder, so it keeps working if the original is moved or deleted. Returns what's wrong with it, or null.
    /// </summary>
    public async Task<string?> SetCustomAlarmAsync(string file)
    {
        long size;
        try
        {
            size = new FileInfo(file).Length;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return AlarmSounds.UnreadableMessage;
        }

        if (size > AlarmSounds.MaximumBytes)
        {
            return AlarmSounds.TooLargeMessage;
        }

        if (await _alarm.MeasureAsync(file) is not { } length || length <= TimeSpan.Zero)
        {
            return AlarmSounds.UnreadableMessage;
        }

        if (length > AlarmSounds.MaximumLength)
        {
            return AlarmSounds.TooLongMessage;
        }

        // The old copy may be the sound that's playing, and Windows won't delete a file that's open.
        StopAlarmSound();

        var extension = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
        var copy = "custom-alarm." + (extension.Length > 0 ? extension : "audio");
        var old = _settings.CustomAlarmFile;

        try
        {
            Directory.CreateDirectory(_settings.Folder);
            File.Copy(file, Path.Combine(_settings.Folder, copy), overwrite: true);

            if (old is not null && old != copy)
            {
                File.Delete(Path.Combine(_settings.Folder, old));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return e.Message;
        }

        _settings.CustomAlarmFile = copy;
        _settings.CustomAlarmName = Path.GetFileName(file);
        _settings.AlarmSound = AlarmSounds.Custom;
        return null;
    }

    // Notification

    /// <summary>The "time's up" notification for the phase that has just finished.</summary>
    public (string Title, string Body) FinishedNotification()
    {
        if (Phase == TimerPhase.Focus)
        {
            var title = CurrentTask.Length == 0 ? "Focus done" : "Focus done: " + CurrentTask;
            return (title, $"Time for a {NextPhase.Title.ToLowerInvariant()}.");
        }

        return ("Break's over", CurrentTask.Length == 0 ? "Ready to focus again?" : "Back to: " + CurrentTask);
    }

    private void PostFinishedNotification()
    {
        var (title, body) = FinishedNotification();
        _notifier.Post(title, body);
    }

    // Task

    /// <summary>Saving on every key would make the app re-apply all its settings each time; save once typing pauses instead.</summary>
    private void ScheduleTaskSave()
    {
        _pendingTaskSave?.Dispose();
        _pendingTaskSave = _scheduler.Schedule(TimeSpan.FromSeconds(0.5), SaveTask);
    }

    /// <summary>Also called when the app quits, so the last keys typed are never lost.</summary>
    public void SaveTask()
    {
        _pendingTaskSave?.Dispose();
        _pendingTaskSave = null;
        _settings.CurrentTask = CurrentTask;
    }

    // Formatting

    /// <summary>Minutes and seconds, like 25:00 or 04:07. Minutes don't wrap, so a long overtime reads 125:03.</summary>
    public static string TimeString(int seconds) =>
        string.Create(CultureInfo.InvariantCulture, $"{seconds / 60:00}:{seconds % 60:00}");

    /// <summary>Text longer than the limit becomes one character less plus "…", counting characters as a reader sees them.</summary>
    private static string Shorten(string text, int limit)
    {
        var info = new StringInfo(text);
        return info.LengthInTextElements > limit ? info.SubstringByTextElements(0, limit - 1) + "…" : text;
    }

    private bool Set<T>(ref T storage, T value, [CallerMemberName] string property = "")
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        Notify(property);
        return true;
    }

    private void Notify(params ReadOnlySpan<string> properties)
    {
        foreach (var property in properties)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }
}
