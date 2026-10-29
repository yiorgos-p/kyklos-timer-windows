namespace Kyklos.Engine;

/// <summary>Runs an action once, after a delay, on the UI thread. Disposing what it returns cancels the action.</summary>
public interface IScheduler
{
    IDisposable Schedule(TimeSpan delay, Action action);
}
