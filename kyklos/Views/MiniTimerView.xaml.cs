using System.ComponentModel;
using System.Globalization;
using Kyklos.Engine;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.System;

namespace Kyklos.Views;

/// <summary>
/// What the mini timer shows, as the Mac's Minimal mode pill does: a small ring of the time left, the time, what the
/// session is for, and the main action and close buttons. While stopped, click the time to type the minutes.
/// </summary>
public sealed partial class MiniTimerView : UserControl
{
    // The ring is 18 epx across with a 3 epx line, so the middle of the line runs 7.5 from the centre.
    private const double RingRadius = 7.5;
    private static readonly Point RingCenter = new(9, 9);

    private readonly TimerViewModel _timer;
    private readonly Palette _palette = new();
    private IDisposable? _systemColors;
    private bool _refreshPending;
    private bool _editing;

    public MiniTimerView(TimerViewModel timer)
    {
        _timer = timer;
        InitializeComponent();

        // The ring and the button for the next phase take the phase colour, as the dial and the main button do.
        Arc.Stroke = _palette.Phase;
        NextPhaseButton.Background = _palette.Button;
        NextPhaseButton.Resources["AccentButtonBackgroundPointerOver"] = _palette.ButtonPointerOver;
        NextPhaseButton.Resources["AccentButtonBackgroundPressed"] = _palette.ButtonPressed;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ActualThemeChanged += (_, _) => UpdatePalette();
    }

    /// <summary>The × button.</summary>
    public event Action? CloseRequested;

    /// <summary>The time was clicked to type the minutes, which needs the keyboard the mini timer otherwise leaves alone.</summary>
    public event Action? KeyboardNeeded;

    /// <summary>Keeps the minutes being typed, if any, as clicking away from them does.</summary>
    public void CommitEditing()
    {
        if (!_editing)
        {
            return;
        }

        if (int.TryParse(MinutesBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes))
        {
            _timer.SetDuration(minutes);
        }

        EndEditing();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _timer.PropertyChanged += OnTimerChanged;
        _systemColors = Palette.WatchSystemColors(DispatcherQueue, UpdatePalette);
        Refresh();
    }

    // The timer outlives this view (the mini timer is thrown away when closed), so let go of it.
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.PropertyChanged -= OnTimerChanged;
        _systemColors?.Dispose();
        _systemColors = null;
    }

    /// <summary>Several properties change together on each tick and each click; redraw once, after they all have.</summary>
    private void OnTimerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_refreshPending)
        {
            return;
        }

        _refreshPending = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _refreshPending = false;
            Refresh();
        });
    }

    private void Refresh()
    {
        var state = _timer.State;
        UpdatePalette();

        // Stepped once a second while running, like the dial: too little to see move in between.
        Arc.Data = RingGeometry.Arc(RingCenter, RingRadius, _timer.DialFraction);
        VisualStateManager.GoToState(this, state switch
        {
            TimerState.Paused => "Paused",
            TimerState.Finished => "TimesUp",
            _ => "Ready",
        }, false);

        TimeText.Text = _timer.DisplayTime;
        ToolTipService.SetToolTip(TimeText, state == TimerState.Stopped ? "Click to type the minutes" : null);
        CaptionText.Text = _editing ? "min" : _timer.MiniTimerCaption;

        // Once time's up, the button starts the next phase and says which, in its colour.
        var finished = state == TimerState.Finished;
        PlayPauseButton.Visibility = finished ? Visibility.Collapsed : Visibility.Visible;
        NextPhaseButton.Visibility = finished ? Visibility.Visible : Visibility.Collapsed;

        var action = state == TimerState.Running ? "Pause" : "Start";
        PlayPauseIcon.Glyph = state == TimerState.Running ? Glyphs.PauseBold : Glyphs.PlaySolid;
        ToolTipService.SetToolTip(PlayPauseButton, action);
        AutomationProperties.SetName(PlayPauseButton, action);

        NextPhaseLabel.Text = _timer.NextPhase == TimerPhase.Focus ? "Focus" : "Break";
        ToolTipService.SetToolTip(NextPhaseButton, _timer.PrimaryActionTitle);
        AutomationProperties.SetName(NextPhaseButton, _timer.PrimaryActionTitle);

        // The length can only be typed before a session starts.
        if (_editing && state != TimerState.Stopped)
        {
            EndEditing();
        }
    }

    private void UpdatePalette()
    {
        var buttonPhase = _timer.State == TimerState.Finished ? _timer.NextPhase : _timer.Phase;
        _palette.Update(_timer.Phase, buttonPhase, ActualTheme);
    }

    private void OnActionClicked(object sender, RoutedEventArgs e) => _timer.ToggleTimer();

    private void OnCloseClicked(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();

    // Typing the minutes

    /// <summary>While stopped the time is for typing the minutes; otherwise a press on it moves the window, as anywhere.</summary>
    private void OnTimePressed(object sender, PointerRoutedEventArgs e)
    {
        if (_timer.State != TimerState.Stopped)
        {
            return;
        }

        e.Handled = true;
        BeginEditing();
    }

    private void BeginEditing()
    {
        _editing = true;
        MinutesBox.Text = _timer.TimerDuration.ToString(CultureInfo.InvariantCulture);
        TimeText.Visibility = Visibility.Collapsed;
        MinutesBox.Visibility = Visibility.Visible;
        CaptionText.Text = "min";

        KeyboardNeeded?.Invoke();
        MinutesBox.Focus(FocusState.Programmatic);
        MinutesBox.SelectAll();
    }

    /// <summary>A box for two digits has no room for the clear button a text box shows while it's typed in.</summary>
    private void OnMinutesBoxLoaded(object sender, RoutedEventArgs e)
    {
        MinutesBox.ApplyTemplate();
        if (FindNamed(MinutesBox, "DeleteButton") is { } clear)
        {
            clear.MaxWidth = 0;
        }
    }

    private void OnMinutesTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args) =>
        args.Cancel = !args.NewText.All(char.IsAsciiDigit);

    private void OnMinutesKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Enter:
                CommitEditing();
                e.Handled = true;
                break;
            case VirtualKey.Escape:
                EndEditing();
                e.Handled = true;
                break;
        }
    }

    private void OnMinutesLostFocus(object sender, RoutedEventArgs e) => CommitEditing();

    private void EndEditing()
    {
        _editing = false;
        MinutesBox.Visibility = Visibility.Collapsed;
        TimeText.Visibility = Visibility.Visible;
        CaptionText.Text = _timer.MiniTimerCaption;

        // Off the hidden box, but not onto a button, where Space or Enter would start the timer.
        Focus(FocusState.Pointer);
    }

    private static FrameworkElement? FindNamed(DependencyObject parent, string name)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is FrameworkElement element && element.Name == name)
            {
                return element;
            }

            if (FindNamed(child, name) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
