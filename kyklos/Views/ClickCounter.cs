using Microsoft.UI.Input;
using Windows.Foundation;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Kyklos.Views;

/// <summary>Counts clicks the way Windows counts a double-click: within its time, and close to the last one.</summary>
internal sealed class ClickCounter
{
    private ulong _lastTime;
    private Point _lastPoint;
    private int _count;

    /// <summary>Counts a press: 1 for a single click, 2 for the second of a double-click, and so on.</summary>
    public int Press(PointerPoint point, double scale)
    {
        var slopX = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXDOUBLECLK) / scale / 2;
        var slopY = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYDOUBLECLK) / scale / 2;
        var withinTime = point.Timestamp - _lastTime <= PInvoke.GetDoubleClickTime() * 1000UL;
        var nearby = Math.Abs(point.Position.X - _lastPoint.X) <= slopX && Math.Abs(point.Position.Y - _lastPoint.Y) <= slopY;

        _count = withinTime && nearby ? _count + 1 : 1;
        _lastTime = point.Timestamp;
        _lastPoint = point.Position;
        return _count;
    }

    /// <summary>The next press starts a new count.</summary>
    public void Reset() => _count = 0;
}
