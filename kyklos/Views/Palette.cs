using Kyklos.Engine;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Kyklos.Views;

/// <summary>
/// The colours that mean phase, as brushes a view paints with. Focus follows the Windows accent colour, as it follows the
/// macOS accent colour on the Mac; the breaks are fixed colours tuned for light and dark (Theme.xaml); high contrast uses
/// the system highlight. The brushes stay the same objects and only change colour, so everything painted with them
/// follows, including controls that take them as theme resources. (Time's up is always orange: OvertimeBrush.)
/// </summary>
public sealed class Palette
{
    private static readonly UISettings System = new();
    private static readonly AccessibilitySettings Accessibility = new();

    /// <summary>The current phase: the arc, the knob's dot, the selected phase and Zen mode's label.</summary>
    public SolidColorBrush Phase { get; } = new();

    public SolidColorBrush PhasePointerOver { get; } = new() { Opacity = 0.9 };

    public SolidColorBrush PhasePressed { get; } = new() { Opacity = 0.8 };

    /// <summary>The main button: the current phase, or the next one once time's up, since that's what it starts.</summary>
    public SolidColorBrush Button { get; } = new();

    public SolidColorBrush ButtonPointerOver { get; } = new() { Opacity = 0.9 };

    public SolidColorBrush ButtonPressed { get; } = new() { Opacity = 0.8 };

    public void Update(TimerPhase phase, TimerPhase buttonPhase, ElementTheme theme)
    {
        Phase.Color = PhasePointerOver.Color = PhasePressed.Color = ColorOf(phase, theme);
        Button.Color = ButtonPointerOver.Color = ButtonPressed.Color = ColorOf(buttonPhase, theme);
    }

    /// <summary>
    /// Calls back on the UI thread when Windows' accent colour changes or high contrast is turned on or off, until
    /// disposed. (AccessibilitySettings.HighContrastChanged needs a UWP window; switching high contrast changes the
    /// system colours too, which this reports.)
    /// </summary>
    public static IDisposable WatchSystemColors(Microsoft.UI.Dispatching.DispatcherQueue queue, Action changed)
    {
        void OnColors(UISettings sender, object args) => queue.TryEnqueue(() => changed());

        System.ColorValuesChanged += OnColors;
        return new Unsubscribe(() => System.ColorValuesChanged -= OnColors);
    }

    private static Color ColorOf(TimerPhase phase, ElementTheme theme) => phase switch
    {
        TimerPhase.ShortBreak => Lookup("ShortBreakColor", theme),
        TimerPhase.LongBreak => Lookup("LongBreakColor", theme),
        _ => Accessibility.HighContrast
            ? System.UIElementColor(UIElementType.Highlight)
            // The shades Windows' own accent fills use: darker on light, lighter on dark.
            : System.GetColorValue(theme == ElementTheme.Dark ? UIColorType.AccentLight2 : UIColorType.AccentDark1),
    };

    private static Color Lookup(string key, ElementTheme theme)
    {
        if (Accessibility.HighContrast)
        {
            return System.UIElementColor(UIElementType.Highlight);
        }

        var themeKey = theme == ElementTheme.Dark ? "Dark" : "Light";

        foreach (var merged in Application.Current.Resources.MergedDictionaries)
        {
            if (merged.ThemeDictionaries.TryGetValue(themeKey, out var themed)
                && ((ResourceDictionary)themed).TryGetValue(key, out var color))
            {
                return (Color)color;
            }
        }

        throw new KeyNotFoundException($"{key} isn't in Theme.xaml's {themeKey} colours.");
    }

    private sealed class Unsubscribe(Action unsubscribe) : IDisposable
    {
        public void Dispose() => unsubscribe();
    }
}
