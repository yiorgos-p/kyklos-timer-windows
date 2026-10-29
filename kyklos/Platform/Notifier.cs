using System.Runtime.InteropServices;
using Kyklos.Engine;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace Kyklos.Platform;

/// <summary>Posts kyklos's one notification, "time's up". It's silent: kyklos plays its own alarm.</summary>
public sealed class Notifier : INotifier
{
    /// <summary>The same tag each time, so a new notification replaces the previous one.</summary>
    private const string Tag = "timer-finished";

    private readonly bool _registered;

    /// <summary>Registers with Windows; clicking a notification then calls <paramref name="clicked"/> on a background thread.</summary>
    public Notifier(Action clicked)
    {
        // The handler has to be in place before Register, or Windows starts another kyklos to handle the click.
        AppNotificationManager.Default.NotificationInvoked += (_, _) => clicked();

        try
        {
            // Named here, or Windows takes the name from the app ID and heads notifications "Kyklos".
            AppNotificationManager.Default.Register("kyklos", new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "kyklos-256.png")));
            _registered = true;
        }
        catch (COMException)
        {
            // Without notifications the alarm still rings; a timer shouldn't stop working over this.
        }
    }

    public void Post(string title, string body)
    {
        if (!_registered)
        {
            return;
        }

        var notification = new AppNotificationBuilder()
            .AddText(title)
            .AddText(body)
            .MuteAudio()
            .BuildNotification();
        notification.Tag = Tag;
        notification.Group = Tag;

        AppNotificationManager.Default.Show(notification);
    }

    /// <summary>On quit, so a click on a notification left on screen launches kyklos again.</summary>
    public void Unregister()
    {
        if (_registered)
        {
            AppNotificationManager.Default.Unregister();
        }
    }
}
