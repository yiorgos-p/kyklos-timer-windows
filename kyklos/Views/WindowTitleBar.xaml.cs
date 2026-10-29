using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Graphics;

namespace Kyklos.Views;

/// <summary>
/// kyklos's title bar, drawn in the content so Mica runs edge to edge: the app icon and the window's name, and on the
/// main window the "Keep on top" pin, as in Calculator.
/// </summary>
public sealed partial class WindowTitleBar : UserControl
{
    private int _iconSize;

    public WindowTitleBar()
    {
        InitializeComponent();
        PinButton.Click += (_, _) => PinToggled?.Invoke(PinButton.IsChecked == true);
    }

    public event Action<bool>? PinToggled;

    public string Title
    {
        get => TitleText.Text;
        set => TitleText.Text = value;
    }

    /// <summary>Only the main window has the pin.</summary>
    public bool ShowsPin
    {
        get => PinButton.Visibility == Visibility.Visible;
        set => PinButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

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

    /// <summary>
    /// Makes this the window's title bar: Windows' caption buttons get room on the right, the pin takes clicks rather
    /// than dragging the window, and the title fades while the window is inactive, as Windows' own title bars do.
    /// </summary>
    public void Attach(Window window)
    {
        window.ExtendsContentIntoTitleBar = true;
        window.SetTitleBar(this);
        SizeChanged += (_, _) => Update(window.AppWindow);
        window.Activated += (_, e) =>
        {
            var active = e.WindowActivationState != WindowActivationState.Deactivated;
            VisualStateManager.GoToState(this, active ? "Active" : "Inactive", false);

            // The caption buttons' room is only known once the window is up.
            Update(window.AppWindow);
        };
    }

    private void Update(AppWindow window)
    {
        if (XamlRoot is null)
        {
            return;
        }

        var scale = XamlRoot.RasterizationScale;
        LeftInsetColumn.Width = new GridLength(window.TitleBar.LeftInset / scale);
        RightInsetColumn.Width = new GridLength(window.TitleBar.RightInset / scale);
        ShowIcon(scale);

        RectInt32[] passthrough = [];
        if (ShowsPin)
        {
            var pin = PinButton.TransformToVisual(null).TransformBounds(new Rect(0, 0, PinButton.ActualWidth, PinButton.ActualHeight));
            passthrough =
            [
                new RectInt32(
                    (int)Math.Round(pin.X * scale),
                    (int)Math.Round(pin.Y * scale),
                    (int)Math.Round(pin.Width * scale),
                    (int)Math.Round(pin.Height * scale)),
            ];
        }

        InputNonClientPointerSource.GetForWindowId(window.Id).SetRegionRects(NonClientRegionKind.Passthrough, passthrough);
    }

    /// <summary>The app icon drawn for the size it shows at: small sizes leave out detail so they stay crisp.</summary>
    private void ShowIcon(double scale)
    {
        int[] sizes = [16, 20, 24, 32, 40, 48];
        var size = sizes.FirstOrDefault(available => available >= Math.Round(16 * scale), sizes[^1]);

        if (size != _iconSize)
        {
            _iconSize = size;
            Icon.Source = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", $"kyklos-{size}.png")));
        }
    }
}
