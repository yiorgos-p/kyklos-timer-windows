using System.Globalization;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Kyklos.Platform;

/// <summary>What a key is called on the keyboard layout in use, for showing a shortcut.</summary>
internal static class KeyNames
{
    /// <summary>The character the key types, or for a key that types nothing (Insert, Pause …) the name Windows gives it.</summary>
    public static string OnLayout(int key)
    {
        // The top bit marks a dead key, like the accent keys on many layouts; the character is in the rest.
        var character = PInvoke.MapVirtualKey((uint)key, MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_CHAR) & 0x7FFFFFFF;
        if (character != 0)
        {
            return ((char)character).ToString();
        }

        // A scan code from the extended set (E0 xx) needs the extended bit, or Insert reads as the number pad's 0.
        var scan = PInvoke.MapVirtualKey((uint)key, MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC_EX);
        var lParam = (int)((scan & 0xFF) << 16 | ((scan & 0xFF00) == 0xE000 ? 1u << 24 : 0));
        Span<char> name = stackalloc char[64];
        var length = PInvoke.GetKeyNameText(lParam, name);
        return length > 0 ? name[..length].ToString() : "0x" + key.ToString("X2", CultureInfo.InvariantCulture);
    }
}
