namespace Kyklos.Engine;

/// <summary>Plays the alarm sound.</summary>
public interface IAlarmPlayer
{
    /// <summary>Stops whatever is playing, then plays the first of these files that opens.</summary>
    void Play(IReadOnlyList<string> files);

    void Stop();

    /// <summary>How long a sound file plays, or null if it can't be played.</summary>
    Task<TimeSpan?> MeasureAsync(string file);

    /// <summary>The sound played to its end, or none of the files would play.</summary>
    event Action? Ended;
}
