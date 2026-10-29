using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;

namespace Kyklos.Views;

/// <summary>The main window's title bar: the app icon and name, and the "Keep on top" pin, as in Calculator.</summary>
public sealed partial class WindowTitleBar : UserControl
{
    public WindowTitleBar()
    {
        InitializeComponent();
        PinButton.Click += (_, _) => PinToggled?.Invoke(PinButton.IsChecked == true);
    }

    public event Action<bool>? PinToggled;

    public bool IsPinned
    {
        set
        {
            PinButton.IsChecked = value;
            PinIcon.Glyph = value ? Glyphs.PinFill : Glyphs.Pin;
            ToolTipService.SetToolTip(PinButton,
                value ? "Stop keeping this window above other windows" : "Keep this window above other windows");
        }
    }

    /// <summary>Where the pin is, in this window's coordinates, so it can be made to take clicks.</summary>
    public Rect PinBounds =>
        PinButton.TransformToVisual(null).TransformBounds(new Rect(0, 0, PinButton.ActualWidth, PinButton.ActualHeight));

    public void SetActive(bool active) => VisualStateManager.GoToState(this, active ? "Active" : "Inactive", false);

    /// <summary>Space kept clear on each side for Windows' caption buttons, in epx.</summary>
    public void SetInsets(double left, double right)
    {
        LeftInsetColumn.Width = new GridLength(left);
        RightInsetColumn.Width = new GridLength(right);
    }

    /// <summary>The app icon drawn for the size it shows at: small sizes leave out detail so they stay crisp.</summary>
    public void SetIconScale(double scale)
    {
        int[] sizes = [16, 20, 24, 32, 40, 48];
        var size = sizes.FirstOrDefault(available => available >= Math.Round(16 * scale), sizes[^1]);
        Icon.Source = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", $"kyklos-{size}.png")));
    }
}
