using Microsoft.UI.Input;
using Microsoft.Win32;

namespace Kyklos.Platform;

/// <summary>
/// Which way the fingers or the wheel really moved. Windows can reverse scrolling, as macOS's natural scrolling does,
/// and the dial should add a minute when moved up either way, so it undoes that.
/// </summary>
public static class ScrollDirection
{
    /// <summary>Whether Windows is reversing the scroll from this kind of device.</summary>
    public static bool IsReversed(PointerDeviceType device) => device == PointerDeviceType.Touchpad
        // A precision touchpad's "Down motion scrolls up" is 0, and the default when unset.
        ? ReadNumber(@"Software\Microsoft\Windows\CurrentVersion\PrecisionTouchPad", "ScrollDirection") is null or 0
        // A mouse wheel is only reversed by Settings → Mouse → "Scrolling direction".
        : ReadNumber(@"Control Panel\Mouse", "ReverseMouseWheelDirection") is 1;

    private static int? ReadNumber(string key, string name)
    {
        using var settings = Registry.CurrentUser.OpenSubKey(key);
        return settings?.GetValue(name) switch
        {
            int number => number,
            string text when int.TryParse(text, out var number) => number,
            _ => null,
        };
    }
}
