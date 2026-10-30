using Windows.Win32;
using Windows.Win32.System.Power;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Kyklos.Platform;

/// <summary>
/// Says when time may have moved on without kyklos's timers firing: the PC woke from sleep, the screen came back on,
/// or the clock or time zone changed. kyklos then catches up at once rather than at its next tick.
/// </summary>
internal sealed class TimeJumps : IDisposable
{
    private readonly SystemWindow _window;
    private readonly HPOWERNOTIFY _display;

    public unsafe TimeJumps(SystemWindow window)
    {
        _window = window;
        _window.Received += OnMessage;

        var display = PInvoke.GUID_CONSOLE_DISPLAY_STATE;
        _display = PInvoke.RegisterPowerSettingNotification(window.Handle, &display, REGISTER_NOTIFICATION_FLAGS.DEVICE_NOTIFY_WINDOW_HANDLE);
    }

    public event Action? Happened;

    public void Dispose()
    {
        _window.Received -= OnMessage;
        PInvoke.UnregisterPowerSettingNotification(_display);
    }

    private unsafe void OnMessage(uint message, nuint wParam, nint lParam)
    {
        var jumped = message switch
        {
            PInvoke.WM_TIMECHANGE => true,
            PInvoke.WM_POWERBROADCAST => wParam switch
            {
                PInvoke.PBT_APMRESUMEAUTOMATIC or PInvoke.PBT_APMRESUMESUSPEND => true,

                // The screen's state: 0 off, 1 on, 2 dimmed.
                PInvoke.PBT_POWERSETTINGCHANGE => ((POWERBROADCAST_SETTING*)lParam)->PowerSetting == PInvoke.GUID_CONSOLE_DISPLAY_STATE
                    && ((POWERBROADCAST_SETTING*)lParam)->Data[0] == 1,
                _ => false,
            },
            _ => false,
        };

        // A new time zone comes with WM_TIMECHANGE too, and .NET keeps the old one until told: the local time, when the
        // session ends and when midnight comes would stay on it.
        if (message == PInvoke.WM_TIMECHANGE)
        {
            TimeZoneInfo.ClearCachedData();
        }

        if (jumped)
        {
            Happened?.Invoke();
        }
    }
}
