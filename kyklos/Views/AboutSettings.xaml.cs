using System.Reflection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Kyklos.Views;

/// <summary>Settings → About: the icon, the version, what kyklos is, its licence, and the Mac version.</summary>
public sealed partial class AboutSettings : UserControl
{
    public AboutSettings()
    {
        InitializeComponent();

        var assembly = typeof(AboutSettings).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var build = Version.TryParse(assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version, out var file) ? file.Revision : 0;
        VersionText.Text = $"Version {version} (Build {build})";

        // The icon drawn for the size it shows at, rather than one size scaled.
        Loaded += (_, _) =>
        {
            int[] sizes = [72, 96, 128, 256];
            var size = sizes.FirstOrDefault(available => available >= Math.Round(72 * XamlRoot.RasterizationScale), sizes[^1]);
            Icon.Source = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", $"kyklos-{size}.png")));
        };
    }
}
