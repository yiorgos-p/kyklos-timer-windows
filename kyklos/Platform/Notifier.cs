using Kyklos.Engine;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace Kyklos.Platform;

/// <summary>
/// Posts kyklos's one notification, "time's up". It's silent: kyklos plays its own alarm. Whatever Windows refuses
/// comes as one exception or another (each kind of error has its own), and none of them may stop the timer.
/// </summary>
public sealed class Notifier : INotifier
{
    /// <summary>The same tag each time, so a new notification replaces the previous one.</summary>
    private const string Tag = "timer-finished";

    /// <summary>
    /// Windows refuses a notification bigger than 5 KB, and a task can be any length; a notification shows a line or two
    /// of it anyway. Each line is kept to this many UTF-16 units.
    /// </summary>
    private const int LineLength = 400;

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
        catch (Exception)
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

        try
        {
            var notification = new AppNotificationBuilder()
                .AddText(Utf16Limit.Fit(title, LineLength))
                .AddText(Utf16Limit.Fit(body, LineLength))
                .MuteAudio()
                .BuildNotification();
            notification.Tag = Tag;
            notification.Group = Tag;

            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception)
        {
            // Posted at time's up, with the alarm about to ring: a notification Windows won't take mustn't stop that.
        }
    }

    /// <summary>On quit, so a click on a notification left on screen launches kyklos again.</summary>
    public void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        try
        {
            AppNotificationManager.Default.Unregister();
        }
        catch (Exception)
        {
            // kyklos is quitting all the same; it registers again at its next start.
        }
    }
}
