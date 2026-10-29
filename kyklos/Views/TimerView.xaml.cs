using System.ComponentModel;
using Kyklos.Engine;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using Windows.System;
using Windows.UI.ViewManagement;

namespace Kyklos.Views;

/// <summary>
/// The timer as the window shows it: the phase switcher, the dial, today's sessions, the task and the buttons.
/// </summary>
public sealed partial class TimerView : UserControl
{
    private const string PlayGlyph = "";
    private const string PauseGlyph = "";
    private const string ResetGlyph = "";
    private const string StopGlyph = "";

    private readonly TimerViewModel _timer;
    private readonly Settings _settings;
    private readonly Palette _palette = new();
    private IDisposable? _systemColors;
    private bool _refreshPending;

    // What the main button's icon and Zen mode's label show, so they're only rebuilt when that changes.
    private (TimerState, TimerPhase)? _startIconShows;
    private TimerPhase? _zenLabelShows;

    public TimerView(TimerViewModel timer, Settings settings)
    {
        _timer = timer;
        _settings = settings;
        InitializeComponent();

        // The selected phase and the main button take the phase colour. Both controls read these brushes as theme
        // resources when their templates load; the palette then recolours the same brushes as the phase changes.
        PhaseSwitcher.Resources["SegmentedPillBackground"] = _palette.Phase;
        PhaseSwitcher.Resources["SegmentedPillBackgroundPointerOver"] = _palette.PhasePointerOver;
        PhaseSwitcher.Resources["SegmentedPillBackgroundPressed"] = _palette.PhasePressed;
        StartButton.Background = _palette.Button;
        StartButton.Resources["AccentButtonBackgroundPointerOver"] = _palette.ButtonPointerOver;
        StartButton.Resources["AccentButtonBackgroundPressed"] = _palette.ButtonPressed;
        ZenTitle.Foreground = _palette.Phase;

        if (!new UISettings().AnimationsEnabled)
        {
            PhaseSwitcher.OpacityTransition = null;
            ZenLabel.OpacityTransition = null;
        }

        Dial.Attach(timer, settings, _palette);

        // Clicking anywhere outside a text field takes the focus off it, so Space starts the timer again.
        AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerPressedAnywhere), handledEventsToo: true);

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ActualThemeChanged += (_, _) => UpdatePalette();
    }

    /// <summary>Raised when the Settings button is clicked.</summary>
    public event Action? SettingsRequested;

    /// <summary>
    /// Puts the keyboard focus on the dial, where Space starts the timer and the arrows set its length. A window never
    /// opens with the cursor in the task field, which would swallow Space. Moved like this, as if by a click, the focus
    /// shows no ring; tabbing to the dial does.
    /// </summary>
    public void FocusDial() => Dial.Focus(FocusState.Pointer);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _timer.PropertyChanged += OnChanged;
        _settings.PropertyChanged += OnChanged;
        _systemColors = Palette.WatchSystemColors(DispatcherQueue, UpdatePalette);
        Refresh();
    }

    // The timer outlives this view (windows are thrown away when hidden), so let go of it.
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.PropertyChanged -= OnChanged;
        _settings.PropertyChanged -= OnChanged;
        _systemColors?.Dispose();
        _systemColors = null;
    }

    /// <summary>Several properties change together on each tick and each click; redraw once, after they all have.</summary>
    private void OnChanged(object? sender, PropertyChangedEventArgs e)
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

        PhaseSwitcher.SelectedIndex = (int)_timer.Phase;

        var zen = _settings.ZenMode && state == TimerState.Running;
        PhaseSwitcher.Opacity = zen ? 0 : 1;
        PhaseSwitcher.IsEnabled = !zen;
        ZenLabel.Opacity = zen ? 1 : 0;
        if (_zenLabelShows != _timer.Phase)
        {
            _zenLabelShows = _timer.Phase;
            var icon = PhaseIcons.Create(_timer.Phase);
            icon.Foreground = _palette.Phase;
            ZenIcon.Child = icon;
            ZenTitle.Text = _timer.Phase.Title;
        }

        Dial.Refresh();
        RefreshSessions();

        if (TaskBox.Text != _timer.CurrentTask)
        {
            TaskBox.Text = _timer.CurrentTask;
        }

        // Mouse mode starts and resets from the dial, so these two buttons go and the icon buttons are centred, as on
        // the Mac. Otherwise the two share the width, except that "Start short break" needs more of it than "Stop".
        var mouse = _settings.UseMouseControls;
        var share = new GridLength(1, GridUnitType.Star);
        StartButton.Visibility = mouse ? Visibility.Collapsed : Visibility.Visible;
        ResetButton.Visibility = StartButton.Visibility;
        StartColumn.Width = mouse || state == TimerState.Finished ? GridLength.Auto : share;
        ResetColumn.Width = mouse ? GridLength.Auto : share;
        Buttons.ColumnSpacing = mouse ? 0 : 8;
        Buttons.HorizontalAlignment = mouse ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        StartLabel.Text = _timer.PrimaryActionTitle;
        ResetLabel.Text = _timer.ResetActionTitle;
        AutomationProperties.SetName(StartButton, _timer.PrimaryActionTitle);
        AutomationProperties.SetName(ResetButton, _timer.ResetActionTitle);
        ResetIcon.Glyph = state == TimerState.Finished ? StopGlyph : ResetGlyph;

        var startIcon = (state, _timer.NextPhase);
        if (_startIconShows != startIcon)
        {
            _startIconShows = startIcon;
            StartIcon.Child = state switch
            {
                // Once time's up the button starts the next phase, so it shows that phase's icon.
                TimerState.Finished => PhaseIcons.Create(_timer.NextPhase),
                TimerState.Running => new FontIcon { Glyph = PauseGlyph },
                _ => new FontIcon { Glyph = PlayGlyph },
            };
        }
    }

    private void UpdatePalette()
    {
        var buttonPhase = _timer.State == TimerState.Finished ? _timer.NextPhase : _timer.Phase;
        _palette.Update(_timer.Phase, buttonPhase, ActualTheme);
    }

    private void RefreshSessions()
    {
        var completed = _timer.CompletedFocusToday;
        var perCycle = _settings.SessionsPerLongBreak;
        var filled = SessionDots.Filled(completed, perCycle);

        while (Dots.Children.Count < perCycle)
        {
            Dots.Children.Add(new Ellipse());
        }

        while (Dots.Children.Count > perCycle)
        {
            Dots.Children.RemoveAt(Dots.Children.Count - 1);
        }

        for (var index = 0; index < perCycle; index++)
        {
            ((Ellipse)Dots.Children[index]).Style = (Style)Resources[index < filled ? "FilledDotStyle" : "EmptyDotStyle"];
        }

        TodayLabel.Text = completed > 0 ? $"{completed} today" : "";
        TodayLabel.Visibility = completed > 0 ? Visibility.Visible : Visibility.Collapsed;
        ResetSessionsButton.Visibility = completed > 0 ? Visibility.Visible : Visibility.Collapsed;
        ResetSessionsItem.IsEnabled = completed > 0;

        var description = SessionDots.Description(completed, perCycle);
        ToolTipService.SetToolTip(Sessions, description);
        AutomationProperties.SetName(Sessions, description);
    }

    private void OnPhaseSelected(object sender, SelectionChangedEventArgs e)
    {
        var phase = (TimerPhase)PhaseSwitcher.SelectedIndex;
        if (PhaseSwitcher.SelectedIndex >= 0 && phase != _timer.Phase)
        {
            _timer.SetPhase(phase);
        }
    }

    private void OnStartClicked(object sender, RoutedEventArgs e) => _timer.ToggleTimer();

    private void OnResetClicked(object sender, RoutedEventArgs e) => _timer.ResetTimer();

    private void OnResetSessions(object sender, RoutedEventArgs e) => _timer.ResetTodaysSessions();

    private void OnSettingsClicked(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();

    private void OnTaskChanged(object sender, TextChangedEventArgs e) => _timer.CurrentTask = TaskBox.Text;

    // Enter or Esc leaves the task field.
    private void OnTaskKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.Enter or VirtualKey.Escape)
        {
            FocusDial();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Space starts and pauses the timer, as the main button's shortcut, unless it's being typed. As on the Mac it goes
    /// with that button, so it does nothing in mouse mode, where the button is hidden.
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Space || StartButton.Visibility != Visibility.Visible
            || FocusManager.GetFocusedElement(XamlRoot) is TextBox)
        {
            return;
        }

        _timer.ToggleTimer();
        e.Handled = true;
    }

    private void OnPointerPressedAnywhere(object sender, PointerRoutedEventArgs e)
    {
        if (!IsInTextBox(e.OriginalSource as DependencyObject))
        {
            FocusDial();
        }
    }

    private static bool IsInTextBox(DependencyObject? element)
    {
        for (var current = element; current is not null; current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current))
        {
            if (current is TextBox)
            {
                return true;
            }
        }

        return false;
    }
}
