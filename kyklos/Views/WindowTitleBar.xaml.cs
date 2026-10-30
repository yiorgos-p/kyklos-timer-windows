using Microsoft.UI.Dispatching;
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

    /// <summary>The window it's the title bar of, until that closes.</summary>
    private Window? _window;

    // The display scale the title bar was last laid out for.
    private double _scale;

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
        _window = window;

        SizeChanged += (_, _) => UpdateOpen();

        // On a screen with another scale, the pin is somewhere else in screen pixels though its size here is the same.
        Loaded += (_, _) =>
        {
            XamlRoot.Changed -= OnXamlRootChanged;
            XamlRoot.Changed += OnXamlRootChanged;
        };
        window.Activated += OnWindowActivated;
        window.Closed += OnWindowClosed;
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs e)
    {
        if (_window is not { } window)
        {
            return;
        }

        var active = e.WindowActivationState != WindowActivationState.Deactivated;
        VisualStateManager.GoToState(this, active ? "Active" : "Inactive", false);

        // The caption buttons' room is only known once the window is up.
        Update(window.AppWindow);
    }

    // There's no title bar left to update once the window has closed. Its handlers go too: WinUI keeps a window's
    // handlers after it has closed, and these refer to the window, so it would never be freed.
    private void OnWindowClosed(object sender, WindowEventArgs e)
    {
        if (_window is { } window)
        {
            _window = null;
            window.Activated -= OnWindowActivated;
            window.Closed -= OnWindowClosed;
        }
    }

    // It changes as the window is shown, hidden and sized too; only a new scale moves the pin.
    private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs e)
    {
        if (sender.RasterizationScale != _scale)
        {
            UpdateOpen();
        }
    }

    private void UpdateOpen()
    {
        if (_window is { } open)
        {
            Update(open.AppWindow);
        }
    }

    private void Update(AppWindow window)
    {
        if (XamlRoot is null)
        {
            return;
        }

        var scale = _scale = XamlRoot.RasterizationScale;
        LeftInsetColumn.Width = new GridLength(window.TitleBar.LeftInset / scale);
        RightInsetColumn.Width = new GridLength(window.TitleBar.RightInset / scale);
        ShowIcon(scale);

        // The new insets move the pin: it's measured once they have, after the layout they cause.
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (_window is not null && XamlRoot is not null)
            {
                LetPinThrough(window, scale);
            }
        });
    }

    /// <summary>The pin takes clicks, rather than them dragging the window as the rest of the title bar does.</summary>
    private void LetPinThrough(AppWindow window, double scale)
    {
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
