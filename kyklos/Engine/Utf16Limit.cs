using System.Globalization;

namespace Kyklos.Engine;

/// <summary>
/// Windows measures text for its limits in UTF-16 units, and one character can take several (a family emoji takes 11),
/// so text that has to fit one is cut at a whole character, with "…".
/// </summary>
public static class Utf16Limit
{
    public static string Fit(string text, int units)
    {
        if (text.Length <= units)
        {
            return text;
        }

        var cut = StringInfo.ParseCombiningCharacters(text).Last(start => start < units);
        return text[..cut] + "…";
    }
}
