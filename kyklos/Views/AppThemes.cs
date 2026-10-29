using Kyklos.Engine;
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
}
