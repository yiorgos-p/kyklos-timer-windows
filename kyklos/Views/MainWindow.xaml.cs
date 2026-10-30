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

    private static readonly uint TaskbarButtonCreated = PInvoke.RegisterWindowMessage("TaskbarButtonCreated");

    private readonly TimerViewModel _timer;
    private readonly Settings _settings;
    private readonly TimerView _view;
    private readonly OverlappedPresenter _presenter;
    private readonly WindowMessages _messages;
    private readonly WindowMessages _taskbarButton;
    private readonly TaskbarProgress _progress;
    private bool _focusedOnce;
    private bool _progressPending;

    /// <summary>Whether it's following the timer: always, except while hidden.</summary>
    private bool _following = true;

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

        // Progress only takes on a taskbar button that exists, and Explorer makes a new one each time the window is
        // shown, and after Explorer restarts.
        _taskbarButton = new WindowMessages(window, (message, _, _) =>
        {
            if (message == TaskbarButtonCreated)
            {
                _progress.Forget();
                UpdateProgress();
            }

            return false;
        });

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
        AppWindow.Closing += OnClosing;
        Activated += OnActivated;
        Closed += OnClosed;
    }

    public TimerView View => _view;

    /// <summary>On screen and not minimised.</summary>
    public bool IsShowing => AppWindow.IsVisible && _presenter.State != OverlappedPresenterState.Minimized;

    /// <summary>Brings the window back up, out of the taskbar if it was minimised, or back from being hidden.</summary>
    public void BringToFront()
    {
        if (!_following)
        {
            _following = true;
            _timer.PropertyChanged += OnTimerChanged;
            _view.Resume();
        }

        if (_presenter.State == OverlappedPresenterState.Minimized)
        {
            _presenter.Restore();
        }

        if (!WindowChrome.OnScreen(AppWindow.Position, AppWindow.Size))
        {
            PlaceWindow();
        }

        Activate();
    }

    /// <summary>
    /// Out of sight, with Windows' own animation: when the tray flyout or the mini timer takes over (one timer on screen),
    /// and for the close button, as kyklos goes on in the tray. It's kept for next time rather than closed, because WinUI
    /// never frees a window with its own title bar; meanwhile it doesn't follow the timer.
    /// </summary>
    public void Hide()
    {
        if (!_following)
        {
            return;
        }

        _following = false;
        SavePosition();
        AppWindow.Hide();
        _timer.PropertyChanged -= OnTimerChanged;
        _view.Suspend();

        // Shown again, it puts the focus on the dial again, wherever it was left.
        _focusedOnce = false;
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

        if (_settings.MainWindowPos is { } saved && WindowChrome.OnScreen(new PointInt32(saved.X, saved.Y), size))
        {
            AppWindow.Move(new PointInt32(saved.X, saved.Y));
            return;
        }

        var area = DisplayArea.Primary.WorkArea;
        AppWindow.Move(new PointInt32(area.X + (area.Width - size.Width) / 2, area.Y + (area.Height - size.Height) / 2));
    }

    // A minimised window sits far off screen, which is no place to come back to.
    private void SavePosition()
    {
        if (_presenter.State != OverlappedPresenterState.Minimized)
        {
            _settings.MainWindowPos = new WindowPosition(AppWindow.Position.X, AppWindow.Position.Y);
        }
    }

    // Only the settings it shows: others change often (the task typed is saved as it's typed), even while it's hidden.
    private void OnSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Settings.Appearance) or nameof(Settings.KeepWindowOnTop) or nameof(Settings.ShowInTaskbar))
        {
            ApplySettings();
        }
    }

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

    // The close button, Alt+F4 or the taskbar's "Close window": the window goes, and kyklos goes on in the tray.
    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        args.Cancel = true;
        Hide();
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState != WindowActivationState.Deactivated && !_focusedOnce)
        {
            _focusedOnce = true;

            // After WinUI has given the first focus to the first control (the pin), where Space would toggle it.
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, _view.FocusDial);
        }
    }

    // Only on Quit.
    private void OnClosed(object sender, WindowEventArgs args)
    {
        _settings.PropertyChanged -= OnSettingChanged;
        _timer.PropertyChanged -= OnTimerChanged;
        SavePosition();
        _messages.Dispose();
        _taskbarButton.Dispose();
    }
}
