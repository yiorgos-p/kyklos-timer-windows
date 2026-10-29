namespace Kyklos.Engine;

/// <summary>Posts the "time's up" notification.</summary>
public interface INotifier
{
    /// <summary>Shows a silent notification in place of the previous one; kyklos plays its own alarm.</summary>
    void Post(string title, string body);
}
