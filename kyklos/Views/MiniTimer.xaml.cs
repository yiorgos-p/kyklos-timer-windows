using System.ComponentModel;
using Kyklos.Engine;
using Kyklos.Platform;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Kyklos.Views;

/// <summary>
/// The mini timer, the Mac's Minimal mode pill: a small window floating above the others, with no taskbar button and
/// no place in Alt+Tab. Clicking it leaves the keyboard with the app being worked in, except to type the minutes. Drag
/// it anywhere to move it; double-click it for the main window.
/// </summary>
public sealed partial class MiniTimer : Window
{
    // A little wider than the Mac's pill: Windows' 14 epx text needs the room to show "Time's up" beside the Break button.
    private const double ContentWidth = 320;
    private const double ContentHeight = 48;

    /// <summary>The first time, it sits this far in from the top-right corner of the main display's work area.</summary>
    private const double CornerMargin = 16;

    private readonly Settings _settings;
    private readonly MiniTimerView _view;
    private readonly HWND _window;
    private readonly ClickCounter _clicks = new();

    // While it's dragged: where the pointer and the window started, in screen pixels.
    private PointInt32? _dragFrom;
    private PointInt32 _windowFrom;
    private bool _moved;

    public MiniTimer(TimerViewModel timer, Settings settings)
    {
        _settings = settings;
        InitializeComponent();
        _view = new MiniTimerView(timer);
        Content = _view;

        // As the tray flyout's: a context menu's presenter, because a standard one without its title bar is never freed.
        var presenter = OverlappedPresenter.CreateForContextMenu();
        presenter.IsAlwaysOnTop = true;
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;
        _window = new HWND(Win32Interop.GetWindowFromWindowId(AppWindow.Id));
        WindowChrome.RoundCorners(_window);
        WindowChrome.DontActivateOnClick(_window);

        ApplyTheme();
        Place();

        _view.PointerPressed += OnPressed;
        _view.PointerMoved += OnMoved;
        _view.PointerReleased += OnReleased;
        _view.PointerCaptureLost += (_, _) => EndDrag();
        _view.CloseRequested += Close;
        _view.KeyboardNeeded += () => PInvoke.SetForegroundWindow(_window);

        Activated += OnActivated;
        _settings.PropertyChanged += OnSettingChanged;
        Closed += OnClosed;
    }

    /// <summary>A double-click, for the full window.</summary>
    public event Action? OpenWindowRequested;

    /// <summary>Shows it without taking the keyboard from the app being worked in.</summary>
    public void Show() => AppWindow.Show(activateWindow: false);

    private double Scale => PInvoke.GetDpiForWindow(_window) / 96.0;

    /// <summary>Where it was left, if that's still on a screen; otherwise near the top-right corner of the main display.</summary>
    private void Place()
    {
        var scale = Scale;
        WindowChrome.ResizeClient(AppWindow, new SizeInt32(
            (int)Math.Ceiling(ContentWidth * scale),
            (int)Math.Ceiling(ContentHeight * scale)));
        var size = AppWindow.Size;

        if (_settings.MiniTimerPos is { } saved
            && DisplayArea.GetFromRect(new RectInt32(saved.X, saved.Y, size.Width, size.Height), DisplayAreaFallback.None) is not null)
        {
            AppWindow.Move(new PointInt32(saved.X, saved.Y));
            return;
        }

        var area = DisplayArea.Primary.WorkArea;
        var margin = (int)Math.Round(CornerMargin * scale);
        WindowChrome.MoveClient(AppWindow, new PointInt32(area.X + area.Width - AppWindow.ClientSize.Width - margin, area.Y + margin));
    }

    private void OnSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Settings.Appearance))
        {
            ApplyTheme();
        }
    }

    private void ApplyTheme() => _view.RequestedTheme = _settings.Appearance.ToElementTheme();

    // Clicking another app takes the keyboard back. Like clicking away in the window, that keeps the minutes typed.
    private void OnActivated(object sender, WindowActivatedEventArgs e)
    {
        if (e.WindowActivationState == WindowActivationState.Deactivated)
        {
            _view.CommitEditing();
        }
    }

    // Lets go of the settings, and of its handlers on its own events: WinUI keeps those after a window has closed, and
    // they refer to the window, so it would never be freed.
    private void OnClosed(object sender, WindowEventArgs e)
    {
        _settings.PropertyChanged -= OnSettingChanged;
        Activated -= OnActivated;
        Closed -= OnClosed;
    }

    // Moving: a press anywhere but the buttons (and the time while it can be typed) drags the window.

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_view);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (_clicks.Press(point, _view.XamlRoot.RasterizationScale) == 2)
        {
            OpenWindowRequested?.Invoke();
            return;
        }

        _dragFrom = ScreenPoint(e);
        _windowFrom = AppWindow.Position;
        _moved = false;
        _view.CapturePointer(e.Pointer);
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragFrom is not { } from)
        {
            return;
        }

        // A hand that wobbles a little during a click or a double-click isn't dragging: a drag starts once the pointer
        // has gone as far as Windows says one does.
        var to = ScreenPoint(e);
        var dpi = PInvoke.GetDpiForWindow(_window);
        if (!_moved
            && Math.Abs(to.X - from.X) <= PInvoke.GetSystemMetricsForDpi(SYSTEM_METRICS_INDEX.SM_CXDRAG, dpi)
            && Math.Abs(to.Y - from.Y) <= PInvoke.GetSystemMetricsForDpi(SYSTEM_METRICS_INDEX.SM_CYDRAG, dpi))
        {
            return;
        }

        var position = new PointInt32(_windowFrom.X + to.X - from.X, _windowFrom.Y + to.Y - from.Y);
        if (position != AppWindow.Position)
        {
            AppWindow.Move(position);
            _moved = true;
        }
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        _view.ReleasePointerCapture(e.Pointer);
        EndDrag();
    }

    private void EndDrag()
    {
        if (_dragFrom is null)
        {
            return;
        }

        _dragFrom = null;
        if (_moved)
        {
            // A click straight after moving it is a new click, not the second of a double-click.
            _clicks.Reset();
            _settings.MiniTimerPos = new WindowPosition(AppWindow.Position.X, AppWindow.Position.Y);
        }
    }

    /// <summary>Where the pointer is on screen: its place in the window, plus where the window is now, as it moves under it.</summary>
    private PointInt32 ScreenPoint(PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(null).Position;
        var scale = _view.XamlRoot.RasterizationScale;
        return new PointInt32(
            AppWindow.Position.X + (int)Math.Round(point.X * scale),
            AppWindow.Position.Y + (int)Math.Round(point.Y * scale));
    }
}
