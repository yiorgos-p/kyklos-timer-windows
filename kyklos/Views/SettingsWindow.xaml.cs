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

/// <summary>
/// kyklos's settings, in three pages like the Mac's tabs: General, Timer and About. The window is as tall as the page
/// it shows, up to the height of the screen, and every change applies at once.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    private const double ContentWidth = 560;

    /// <summary>A page taller than the screen leaves this much clear above and below the window.</summary>
    private const double ScreenMargin = 16;

    private readonly TimerViewModel _timer;
    private readonly Settings _settings;
    private readonly GlobalHotKey _hotKey;
    private readonly OverlappedPresenter _presenter;
    private readonly WindowMessages _messages;
    private bool _placed;
    private bool _focusedOnce;

    internal SettingsWindow(TimerViewModel timer, Settings settings, GlobalHotKey hotKey)
    {
        _timer = timer;
        _settings = settings;
        _hotKey = hotKey;
        InitializeComponent();

        _presenter = (OverlappedPresenter)AppWindow.Presenter;
        _presenter.IsResizable = false;
        _presenter.IsMaximizable = false;
        _messages = WindowChrome.RefuseMaximize(Win32Interop.GetWindowFromWindowId(AppWindow.Id));
        AppTitleBar.Attach(this);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "kyklos.ico"));
        _settings.Appearance.Apply(this, Root);

        Pages.SelectedItem = Pages.Items[0];
        FitToContent();
        Page.SizeChanged += (_, e) =>
        {
            if (e.NewSize.Height != e.PreviousSize.Height)
            {
                FitToContent();
            }
        };

        _settings.PropertyChanged += OnSettingChanged;
        Activated += (_, e) =>
        {
            if (e.WindowActivationState == WindowActivationState.Deactivated)
            {
                return;
            }

            if (Page.Content is GeneralSettings general)
            {
                general.RefreshNotifications();
            }

            // WinUI gives a new window's first control the focus as if by keyboard, with a focus ring; the page tab
            // takes it as if clicked instead, as when Settings is opened with the mouse.
            if (!_focusedOnce)
            {
                _focusedOnce = true;
                DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => (Pages.SelectedItem as Control)?.Focus(FocusState.Pointer));
            }
        };
        Closed += (_, _) =>
        {
            _settings.PropertyChanged -= OnSettingChanged;
            _messages.Dispose();

            // Lets the page go, which stops a sound it's playing or a shortcut it's recording.
            Page.Content = null;
        };
    }

    public void BringToFront()
    {
        if (_presenter.State == OverlappedPresenterState.Minimized)
        {
            _presenter.Restore();
        }

        Activate();
    }

    private void OnSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Settings.Appearance))
        {
            _settings.Appearance.Apply(this, Root);
        }
    }

    private void OnPageSelected(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        Page.Content = sender.Items.IndexOf(sender.SelectedItem) switch
        {
            0 => new GeneralSettings(_timer, _settings, _hotKey),
            1 => new TimerSettings(_timer, _settings),
            _ => new AboutSettings(),
        };
        Scroller.ChangeView(null, 0, null, disableAnimation: true);
    }

    private double Scale => PInvoke.GetDpiForWindow(new HWND(Win32Interop.GetWindowFromWindowId(AppWindow.Id))) / 96.0;

    /// <summary>
    /// As tall as the page, but no taller than the screen, where the page scrolls instead. The top edge stays where it
    /// is, unless the window would then run off the bottom of the screen.
    /// </summary>
    private void FitToContent()
    {
        Root.Measure(new Size(ContentWidth, double.PositiveInfinity));
        var scale = Scale;
        var work = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var margin = (int)Math.Round(ScreenMargin * scale);
        var frame = AppWindow.Size.Height - AppWindow.ClientSize.Height;
        var height = Math.Min((int)Math.Ceiling(Root.DesiredSize.Height * scale), work.Height - frame - 2 * margin);
        WindowChrome.ResizeClient(AppWindow, new SizeInt32((int)Math.Ceiling(ContentWidth * scale), height));

        // The first time, in the middle of the screen; after that, only moved up if it now runs off the bottom.
        var size = AppWindow.Size;
        var position = AppWindow.Position;
        if (!_placed)
        {
            _placed = true;
            position = new PointInt32(work.X + (work.Width - size.Width) / 2, work.Y + (work.Height - size.Height) / 2);
        }

        var y = Math.Max(work.Y + margin, Math.Min(position.Y, work.Y + work.Height - size.Height - margin));
        AppWindow.Move(new PointInt32(position.X, y));
    }
}
