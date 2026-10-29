using Kyklos.Engine;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace Kyklos.Views;

public static class AppThemes
{
    /// <summary>The theme to request on a window's content: Windows' own, or always light or dark.</summary>
    public static ElementTheme ToElementTheme(this AppTheme theme) => theme switch
    {
        AppTheme.Light => ElementTheme.Light,
        AppTheme.Dark => ElementTheme.Dark,
        _ => ElementTheme.Default,
    };

    /// <summary>Puts the theme on a window with its own title bar: the content, and the caption buttons Windows draws.</summary>
    public static void Apply(this AppTheme theme, Window window, FrameworkElement content)
    {
        content.RequestedTheme = theme.ToElementTheme();
        window.AppWindow.TitleBar.PreferredTheme = theme switch
        {
            AppTheme.Light => TitleBarTheme.Light,
            AppTheme.Dark => TitleBarTheme.Dark,
            _ => TitleBarTheme.UseDefaultAppMode,
        };
    }
}
