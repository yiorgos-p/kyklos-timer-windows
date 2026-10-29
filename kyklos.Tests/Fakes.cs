namespace Kyklos.Tests;

/// <summary>A clock that moves only when a test moves it.</summary>
sealed class FakeClock : IClock
{
    public TimeSpan Now { get; set; } = TimeSpan.FromHours(1);

    public DateTime LocalNow { get; set; } = new(2026, 10, 29, 14, 0, 0);

    /// <summary>Moves both clocks on together, as real time does.</summary>
    public void Advance(TimeSpan time)
    {
        Now += time;
        LocalNow += time;
    }
}

/// <summary>Collects scheduled actions and runs each one when the fake clock reaches it.</summary>
sealed class FakeScheduler(FakeClock clock) : IScheduler
{
    private readonly List<Entry> _entries = [];

    /// <summary>How late each action runs (early, if negative), as real timers are never exact.</summary>
    public TimeSpan Lateness { get; set; }

    public int Pending => _entries.Count;

    /// <summary>How many actions have run.</summary>
    public int Runs { get; private set; }

    public IDisposable Schedule(TimeSpan delay, Action action)
    {
        var entry = new Entry(clock.Now + delay + Lateness, action, _entries);
        _entries.Add(entry);
        return entry;
    }

    /// <summary>Lets time pass, running each action at its moment.</summary>
    public void Advance(TimeSpan time)
    {
        var end = clock.Now + time;

        while (_entries.MinBy(entry => entry.Due) is { } next && next.Due <= end)
        {
            _entries.Remove(next);

            if (next.Due > clock.Now)
            {
                clock.Advance(next.Due - clock.Now);
            }

            Runs++;
            next.Action();
        }

        clock.Advance(end - clock.Now);
    }

    /// <summary>A sleep: time jumps ahead with nothing running, then whatever came due runs late, as after a wake.</summary>
    public void Sleep(TimeSpan time)
    {
        clock.Advance(time);

        foreach (var entry in _entries.Where(entry => entry.Due <= clock.Now).ToList())
        {
            _entries.Remove(entry);
            Runs++;
            entry.Action();
        }
    }

    private sealed class Entry(TimeSpan due, Action action, List<Entry> owner) : IDisposable
    {
        public TimeSpan Due { get; } = due;

        public Action Action { get; } = action;

        public void Dispose() => owner.Remove(this);
    }
}

sealed class FakeAlarm : IAlarmPlayer
{
    /// <summary>The files each Play call was given.</summary>
    public List<IReadOnlyList<string>> Plays { get; } = [];

    public int Stops { get; private set; }

    /// <summary>What MeasureAsync reports for each file; a file not listed can't be played.</summary>
    public Dictionary<string, TimeSpan> Lengths { get; } = new();

    public void Play(IReadOnlyList<string> files) => Plays.Add(files);

    public void Stop() => Stops++;

    public Task<TimeSpan?> MeasureAsync(string file) =>
        Task.FromResult(Lengths.TryGetValue(file, out var length) ? length : (TimeSpan?)null);
}

sealed class FakeNotifier : INotifier
{
    public List<(string Title, string Body)> Posted { get; } = [];

    public void Post(string title, string body) => Posted.Add((title, body));
}

/// <summary>A timer with fake time, sound and notifications, and a settings folder of its own.</summary>
sealed class TestTimer : IDisposable
{
    public TestTimer(Action<Settings>? configure = null)
    {
        Folder = Path.Combine(Path.GetTempPath(), "kyklos-tests", Guid.NewGuid().ToString("N"));
        Scheduler = new FakeScheduler(Clock);
        Settings = new Settings(Folder);
        configure?.Invoke(Settings);
        Timer = new TimerViewModel(Settings, Clock, Scheduler, Alarm, Notifier);
    }

    public string Folder { get; }

    public FakeClock Clock { get; } = new();

    public FakeScheduler Scheduler { get; }

    public FakeAlarm Alarm { get; } = new();

    public FakeNotifier Notifier { get; } = new();

    public Settings Settings { get; }

    public TimerViewModel Timer { get; }

    /// <summary>Lets time pass, to the nearest tick, so 46.4 seconds is exactly that.</summary>
    public void Advance(double seconds) =>
        Scheduler.Advance(TimeSpan.FromTicks((long)Math.Round(seconds * TimeSpan.TicksPerSecond)));

    /// <summary>Starts the current phase and runs it to time's up.</summary>
    public void Finish()
    {
        Timer.StartTimer();
        Advance(Timer.TimerDuration * 60);
    }

    public void Dispose()
    {
        if (Directory.Exists(Folder))
        {
            Directory.Delete(Folder, recursive: true);
        }
    }
}
