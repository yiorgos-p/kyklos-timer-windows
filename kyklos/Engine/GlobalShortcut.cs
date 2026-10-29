using System.Globalization;
using System.Numerics;

namespace Kyklos.Engine;

/// <summary>The global shortcut's modifier keys, with the values Windows' RegisterHotKey takes.</summary>
[Flags]
public enum ShortcutModifiers
{
    None = 0,
    Alt = 0x1,
    Control = 0x2,
    Shift = 0x4,
    Windows = 0x8,
}

/// <summary>
/// The shortcut that starts, pauses and resumes the timer from any app: a key, by its Windows virtual-key code, and its
/// modifiers.
/// </summary>
public readonly record struct GlobalShortcut(int Key, ShortcutModifiers Modifiers)
{
    /// <summary>Ctrl+Alt+K, the Mac's ⌃⌥K. Ctrl+Alt is AltGr on many layouts, but K types nothing with it on most.</summary>
    public static readonly GlobalShortcut Default = new('K', ShortcutModifiers.Control | ShortcutModifiers.Alt);

    public const string InvalidMessage = "Use Ctrl or Win plus at least one more modifier key, like Ctrl+Alt+K.";
    public const string TakenMessage = "Windows wouldn't take this shortcut. Another app may be using it.";

    /// <summary>Two modifiers or more, one of them Ctrl or Win, so it can't get in the way of typing.</summary>
    public bool IsValid =>
        BitOperations.PopCount((uint)Modifiers) >= 2
        && (Modifiers & (ShortcutModifiers.Control | ShortcutModifiers.Windows)) != 0;

    /// <summary>
    /// The shortcut as Windows writes one, like "Ctrl+Alt+K": Win, Ctrl, Alt and Shift in that order, then the key. Keys
    /// that type nothing have names; any other is the character it types on the keyboard layout in use, which
    /// <paramref name="nameOnLayout"/> gives.
    /// </summary>
    public string Describe(Func<int, string> nameOnLayout)
    {
        List<string> parts = [];

        foreach (var (modifier, name) in ModifierNames)
        {
            if (Modifiers.HasFlag(modifier))
            {
                parts.Add(name);
            }
        }

        parts.Add(KeyName(Key) ?? nameOnLayout(Key).ToUpper(CultureInfo.CurrentCulture));
        return string.Join("+", parts);
    }

    private static readonly (ShortcutModifiers Modifier, string Name)[] ModifierNames =
    [
        (ShortcutModifiers.Windows, "Win"),
        (ShortcutModifiers.Control, "Ctrl"),
        (ShortcutModifiers.Alt, "Alt"),
        (ShortcutModifiers.Shift, "Shift"),
    ];

    private static string? KeyName(int key) => key switch
    {
        0x08 => "Backspace",
        0x09 => "Tab",
        0x0D => "Enter",
        0x20 => "Space",
        0x21 => "Page Up",
        0x22 => "Page Down",
        0x23 => "End",
        0x24 => "Home",
        0x25 => "Left",
        0x26 => "Up",
        0x27 => "Right",
        0x28 => "Down",
        0x2E => "Delete",
        >= 0x70 and <= 0x7B => "F" + (key - 0x6F).ToString(CultureInfo.InvariantCulture),
        _ => null,
    };
}
