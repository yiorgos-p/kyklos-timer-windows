using System.ComponentModel;
using Kyklos.Engine;
using Kyklos.Platform;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Kyklos.Views;

/// <summary>The timer in its own window: as wide as the Mac's, only as tall as its content, and never resized by hand.</summary>
public sealed partial class MainWindow : Window
{
    private const double ContentWidth = 360;

    private readonly TimerViewModel _timer;
    private readonly Settings _settings;
    private readonly TimerView _view;
    private readonly OverlappedPresenter _presenter;
    private readonly WindowMessages _messages;
    private readonly TaskbarProgress _progress;
    private bool _focusedOnce;
    private bool _progressPending;

    public MainWindow(TimerViewModel timer, Settings settings)
    {
        _timer = timer;
        _settings = settings;
        InitializeComponent();

        _view = new TimerView(timer, settings, TimerPresentation.Window);
        Grid.SetRow(_view, 1);
        Root.Children.Add(_view);

        // It's always exactly as big as its content, so it can't be resized or maximised.
        _presenter = (OverlappedPresenter)AppWindow.Presenter;
        _presenter.IsResizable = false;
        _presenter.IsMaximizable = false;
        var window = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        _messages = WindowChrome.RefuseMaximize(window);
        _progress = new TaskbarProgress(window);

        // The content runs under the title bar, so Mica covers the window edge to edge, as the Mac window is one glass
        // surface.
        AppTitleBar.Attach(this);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "kyklos.ico"));

        ApplySettings();
        FitToContent();
        PlaceWindow();

        _settings.PropertyChanged += OnSettingChanged;
        _timer.PropertyChanged += OnTimerChanged;
        _view.SizeChanged += (_, e) =>
        {
            if (e.NewSize.Height != e.PreviousSize.Height)
            {
                FitToContent();
            }
        };
        AppTitleBar.PinToggled += pinned => _settings.KeepWindowOnTop = pinned;
        Activated += OnActivated;
        Closed += OnClosed;
    }

    public TimerView View => _view;

    /// <summary>On screen and not minimised.</summary>
    public bool IsShowing => AppWindow.IsVisible && _presenter.State != OverlappedPresenterState.Minimized;

    /// <summary>Brings the window back up, out of the taskbar if it was minimised.</summary>
    public void BringToFront()
    {
        if (_presenter.State == OverlappedPresenterState.Minimized)
        {
            _presenter.Restore();
        }

        Activate();
    }

    /// <summary>
    /// Gives way to the tray flyout, so there's one timer on screen: hidden with Windows' own animation, then let go of,
    /// so it doesn't sit in memory. Opening it again builds a new one where this one was.
    /// </summary>
    public void FadeOutAndClose()
    {
        SavePosition();
        AppWindow.Hide();

        var close = DispatcherQueue.CreateTimer();
        close.Interval = TimeSpan.FromMilliseconds(200);
        close.IsRepeating = false;
        close.Tick += (_, _) => Close();
        close.Start();
    }

    private double Scale => PInvoke.GetDpiForWindow(new HWND(Win32Interop.GetWindowFromWindowId(AppWindow.Id))) / 96.0;

    /// <summary>Sizes the window to its content, keeping the top edge where it is.</summary>
    private void FitToContent()
    {
        Root.Measure(new Size(ContentWidth, double.PositiveInfinity));
        var scale = Scale;
        WindowChrome.ResizeClient(AppWindow, new SizeInt32(
            (int)Math.Ceiling(ContentWidth * scale),
            (int)Math.Ceiling(Root.DesiredSize.Height * scale)));
    }

    /// <summary>Where it was left, if that's still on a screen; otherwise the middle of the main display.</summary>
    private void PlaceWindow()
    {
        var size = AppWindow.Size;

        if (_settings.MainWindowPos is { } saved
            && DisplayArea.GetFromRect(new RectInt32(saved.X, saved.Y, size.Width, size.Height), DisplayAreaFallback.None) is not null)
        {
            AppWindow.Move(new PointInt32(saved.X, saved.Y));
            return;
        }

        var area = DisplayArea.Primary.WorkArea;
        AppWindow.Move(new PointInt32(area.X + (area.Width - size.Width) / 2, area.Y + (area.Height - size.Height) / 2));
    }

    private void SavePosition() => _settings.MainWindowPos = new WindowPosition(AppWindow.Position.X, AppWindow.Position.Y);

    private void OnSettingChanged(object? sender, PropertyChangedEventArgs e) => ApplySettings();

    private void ApplySettings()
    {
        _settings.Appearance.Apply(this, Root);
        _presenter.IsAlwaysOnTop = _settings.KeepWindowOnTop;
        AppTitleBar.IsPinned = _settings.KeepWindowOnTop;

        // Off, kyklos lives in the tray and this window: no taskbar button, and not in Alt+Tab.
        AppWindow.IsShownInSwitchers = _settings.ShowInTaskbar;
        UpdateProgress();
    }

    // Several properties change together on each tick; update the taskbar once, after they all have.
    private void OnTimerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_progressPending)
        {
            return;
        }

        _progressPending = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _progressPending = false;
            UpdateProgress();
        });
    }

    /// <summary>The session's progress on the taskbar button, so the time shows even with the window minimised.</summary>
    private void UpdateProgress() => _progress.Show(_settings.ShowInTaskbar ? _timer : null);

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState != WindowActivationState.Deactivated && !_focusedOnce)
        {
            _focusedOnce = true;

            // After WinUI has given the first focus to the first control (the pin), where Space would toggle it.
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, _view.FocusDial);
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _settings.PropertyChanged -= OnSettingChanged;
        _timer.PropertyChanged -= OnTimerChanged;
        SavePosition();
        _messages.Dispose();
    }
}
