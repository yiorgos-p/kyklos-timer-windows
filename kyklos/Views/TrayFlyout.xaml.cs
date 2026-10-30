using System.Numerics;
using Kyklos.Engine;
using Kyklos.Platform;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI.ViewManagement;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Kyklos.Views;

/// <summary>
/// The timer above the tray icon, as the Mac shows it in a popover from the menu bar: like Windows' own clock and quick
/// settings flyouts, it has no title bar or taskbar button, floats above other windows, and closes when you click
/// elsewhere or press Esc.
/// </summary>
public sealed partial class TrayFlyout : Window
{
    private const double ContentWidth = 360;

    /// <summary>Space between the flyout and the edges of the work area, as Windows' own flyouts leave.</summary>
    private const double Margin = 12;

    private readonly TimerView _view;
    private RECT? _icon;
    private PointInt32 _click;

    public TrayFlyout(TimerViewModel timer, Settings settings)
    {
        InitializeComponent();
        _view = new TimerView(timer, settings, TimerPresentation.Flyout);
        Root.Children.Add(_view);
        Root.RequestedTheme = settings.Appearance.ToElementTheme();

        // A context menu's presenter: a border and no title bar, and it can't be resized, minimised or maximised. (Told
        // SetBorderAndTitleBar(true, false), a standard presenter looks the same, but WinUI then never frees the closed
        // window.)
        var presenter = OverlappedPresenter.CreateForContextMenu();
        presenter.IsAlwaysOnTop = true;
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;
        WindowChrome.RoundCorners(Win32Interop.GetWindowFromWindowId(AppWindow.Id));

        if (!new UISettings().AnimationsEnabled)
        {
            Root.OpacityTransition = null;
            Root.TranslationTransition = null;
        }

        // Light dismiss: clicking anywhere else closes it.
        Activated += OnActivated;
        Closed += OnClosed;

        // Measured before it's first laid out, the size is only an estimate; the first layout corrects it while the
        // flyout is still fading in.
        _view.SizeChanged += (_, e) =>
        {
            if (e.NewSize.Height != e.PreviousSize.Height)
            {
                Place();
            }
        };
    }

    public TimerView View => _view;

    private double Scale => PInvoke.GetDpiForWindow(new HWND(Win32Interop.GetWindowFromWindowId(AppWindow.Id))) / 96.0;

    /// <summary>Opens over the tray icon (or the point clicked, if Windows can't say where the icon is).</summary>
    internal void Open(RECT? icon, PointInt32 click)
    {
        _icon = icon;
        _click = click;

        // Onto the icon's monitor first, so the size is worked out at that monitor's scale.
        AppWindow.Move(Anchor);
        Place();

        // Slide in from the taskbar, as Windows' flyouts do.
        var fromBelow = DisplayArea.GetFromPoint(Anchor, DisplayAreaFallback.Nearest) is var display
            && display.WorkArea.Y == display.OuterBounds.Y;
        Root.Translation = new Vector3(0, fromBelow ? 12 : -12, 0);
        Activate();
        Root.Opacity = 1;
        Root.Translation = Vector3.Zero;

        // A flyout never opens with the cursor in the task field, which would swallow Space.
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, _view.FocusDial);
    }

    private PointInt32 Anchor => _icon is { } icon
        ? new PointInt32((icon.left + icon.right) / 2, (icon.top + icon.bottom) / 2)
        : _click;

    /// <summary>
    /// Sizes it to its content and puts it above the taskbar over the icon, inside the work area of the icon's monitor.
    /// The taskbar can be on any edge; the flyout keeps clear of it.
    /// </summary>
    private void Place()
    {
        var scale = Scale;
        Root.Measure(new Size(ContentWidth, double.PositiveInfinity));
        WindowChrome.ResizeClient(AppWindow, new SizeInt32(
            (int)Math.Ceiling(ContentWidth * scale),
            (int)Math.Ceiling(Root.DesiredSize.Height * scale)));

        var size = AppWindow.ClientSize;
        var display = DisplayArea.GetFromPoint(Anchor, DisplayAreaFallback.Nearest);
        var work = display.WorkArea;
        var screen = display.OuterBounds;
        var margin = (int)Math.Round(Margin * scale);

        var x = Math.Clamp(Anchor.X - size.Width / 2, work.X + margin, work.X + work.Width - size.Width - margin);
        var y = work.Y + work.Height - size.Height - margin;

        if (work.Y > screen.Y)
        {
            y = work.Y + margin;
        }
        else if (work.X > screen.X)
        {
            x = work.X + margin;
        }
        else if (work.X + work.Width < screen.X + screen.Width)
        {
            x = work.X + work.Width - size.Width - margin;
        }

        WindowChrome.MoveClient(AppWindow, new PointInt32(x, y));
    }

    private void OnActivated(object sender, WindowActivatedEventArgs e)
    {
        if (e.WindowActivationState == WindowActivationState.Deactivated)
        {
            Close();
        }
    }

    // WinUI keeps a window's handlers on its own events after it has closed, and these refer to the window, so it would
    // never be freed. Each of kyklos's windows lets go of its own when it closes.
    private void OnClosed(object sender, WindowEventArgs e)
    {
        Activated -= OnActivated;
        Closed -= OnClosed;
    }

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}
