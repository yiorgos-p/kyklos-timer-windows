using System.Runtime.InteropServices;
using Kyklos.Engine;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Kyklos.Platform;

/// <summary>
/// kyklos in the notification area. It's how kyklos is reached when its window is closed, so it's always there; Windows
/// lets people hide it in Settings → Personalization → Taskbar.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private const uint Id = 1;
    private const uint CallbackMessage = PInvoke.WM_APP + 1;

    /// <summary>NIN_KEYSELECT: NIN_SELECT with NINF_KEY, when the icon is chosen from the keyboard.</summary>
    private const uint KeySelect = PInvoke.NIN_SELECT | 1;

    private readonly SystemWindow _window;
    private readonly uint _taskbarCreated = PInvoke.RegisterWindowMessage("TaskbarCreated");
    private (TrayTone Tone, double Fraction) _shown = (TrayTone.Glyph, 0);
    private string _tooltip = "kyklos";
    private HICON _icon;
    private string? _iconShows;

    public TrayIcon(SystemWindow window)
    {
        _window = window;
        _window.Received += OnMessage;
        Add();
    }

    /// <summary>A left click, or Enter or Space while the icon has the keyboard: where on screen, and whether it was the keyboard.</summary>
    public event Action<PointInt32, bool>? Selected;

    /// <summary>A right click, or the menu key: where the menu should open, and whether it was the keyboard.</summary>
    public event Action<PointInt32, bool>? MenuRequested;

    /// <summary>The icon's place on screen, so the flyout can open above it.</summary>
    public RECT? Bounds
    {
        get
        {
            var identifier = new NOTIFYICONIDENTIFIER
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONIDENTIFIER>(),
                hWnd = _window.Handle,
                uID = Id,
            };
            return PInvoke.Shell_NotifyIconGetRect(identifier, out var rect).Succeeded ? rect : null;
        }
    }

    /// <summary>The ring for this tone and fraction, and the tooltip with the time and task.</summary>
    public void Show(TrayTone tone, double fraction, string title)
    {
        _shown = (tone, fraction);
        _tooltip = TrayIndicator.Tooltip(title);
        Notify(NOTIFY_ICON_MESSAGE.NIM_MODIFY);
    }

    public void Dispose()
    {
        _window.Received -= OnMessage;
        Notify(NOTIFY_ICON_MESSAGE.NIM_DELETE);
        PInvoke.DestroyIcon(_icon);
    }

    private void Add()
    {
        Notify(NOTIFY_ICON_MESSAGE.NIM_ADD);

        // Version 4: clicks come with the point they happened at, and keyboard use of the icon is reported.
        var data = Data();
        data.Anonymous.uVersion = PInvoke.NOTIFYICON_VERSION_4;
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_SETVERSION, data);
    }

    private void Notify(NOTIFY_ICON_MESSAGE message)
    {
        if (message != NOTIFY_ICON_MESSAGE.NIM_DELETE)
        {
            RedrawIfChanged();
        }

        var data = Data();
        data.uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON
            | NOTIFY_ICON_DATA_FLAGS.NIF_TIP | NOTIFY_ICON_DATA_FLAGS.NIF_SHOWTIP;
        data.uCallbackMessage = CallbackMessage;
        data.hIcon = _icon;
        data.szTip = _tooltip;
        PInvoke.Shell_NotifyIcon(message, data);
    }

    /// <summary>The icon is only drawn again when it would look different.</summary>
    private void RedrawIfChanged()
    {
        var light = TrayArt.TaskbarIsLight();
        var size = TrayArt.IconSize();
        var shows = $"{_shown.Tone} {TrayIndicator.Step(_shown.Fraction)} {light} {size}";

        if (shows == _iconShows)
        {
            return;
        }

        var previous = _icon;
        _icon = _shown.Tone == TrayTone.Glyph
            ? TrayArt.Glyph(light, size)
            : TrayArt.Ring(_shown.Tone, _shown.Fraction, light, size);
        _iconShows = shows;
        PInvoke.DestroyIcon(previous);
    }

    private NOTIFYICONDATAW Data() => new()
    {
        cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
        hWnd = _window.Handle,
        uID = Id,
    };

    private void OnMessage(uint message, nuint wParam, nint lParam)
    {
        // Explorer restarted, and with it the notification area.
        if (message == _taskbarCreated)
        {
            Add();
            return;
        }

        // The taskbar may have switched between light and dark.
        if (message == PInvoke.WM_SETTINGCHANGE && lParam != 0 && Marshal.PtrToStringUni(lParam) == "ImmersiveColorSet")
        {
            Notify(NOTIFY_ICON_MESSAGE.NIM_MODIFY);
            return;
        }

        if (message != CallbackMessage)
        {
            return;
        }

        var point = new PointInt32((short)(wParam & 0xFFFF), (short)((wParam >> 16) & 0xFFFF));

        var notification = (uint)(lParam & 0xFFFF);
        switch (notification)
        {
            case PInvoke.NIN_SELECT or KeySelect:
                Selected?.Invoke(point, notification == KeySelect);
                break;
            case PInvoke.WM_CONTEXTMENU:
                MenuRequested?.Invoke(point, !IsPointerAt(point));
                break;
        }
    }

    /// <summary>
    /// The shell sends the same message for a right click and for the menu key. A right click comes with the point
    /// clicked, where the pointer still is; the keyboard, with the icon's corner.
    /// </summary>
    private static bool IsPointerAt(PointInt32 point)
    {
        PInvoke.GetCursorPos(out var pointer);
        var slack = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXDRAG);
        return Math.Abs(pointer.X - point.X) <= slack && Math.Abs(pointer.Y - point.Y) <= slack;
    }
}
