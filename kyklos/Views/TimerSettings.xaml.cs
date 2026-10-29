using System.ComponentModel;
using Kyklos.Engine;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Kyklos.Views;

/// <summary>Settings → Timer: how long each phase is, and the rhythm of focus sessions and long breaks.</summary>
public sealed partial class TimerSettings : UserControl
{
    private readonly TimerViewModel _timer;
    private readonly Settings _settings;

    /// <summary>While the boxes are being set from the settings, so they don't write them straight back.</summary>
    private bool _updating;

    public TimerSettings(TimerViewModel timer, Settings settings)
    {
        _timer = timer;
        _settings = settings;
        InitializeComponent();

        Loaded += (_, _) =>
        {
            _settings.PropertyChanged += OnSettingChanged;
            _timer.PropertyChanged += OnTimerChanged;
            Refresh();
        };
        Unloaded += (_, _) =>
        {
            _settings.PropertyChanged -= OnSettingChanged;
            _timer.PropertyChanged -= OnTimerChanged;
        };
    }

    private void OnSettingChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void OnTimerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TimerViewModel.CompletedFocusToday))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        _updating = true;
        FocusMinutes.Value = _settings.StandardTimer;
        ShortBreakMinutes.Value = _settings.ShortBreak;
        LongBreakMinutes.Value = _settings.LongBreak;
        SessionsPerLongBreak.Value = _settings.SessionsPerLongBreak;

        var finished = _timer.CompletedFocusToday;
        FinishedToday.Header = $"Finished today: {finished}";
        ResetToday.IsEnabled = finished > 0;
        _updating = false;
    }

    private void OnMinutesChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_updating)
        {
            return;
        }

        // An emptied box keeps the length it had.
        if (!double.IsNaN(args.NewValue))
        {
            var minutes = (int)Math.Round(args.NewValue);
            if (sender == FocusMinutes)
            {
                _settings.StandardTimer = minutes;
            }
            else if (sender == ShortBreakMinutes)
            {
                _settings.ShortBreak = minutes;
            }
            else
            {
                _settings.LongBreak = minutes;
            }
        }

        // Shows what was kept, clamped into range, once the box has finished with this change.
        DispatcherQueue.TryEnqueue(Refresh);
    }

    private void OnSessionsChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_updating)
        {
            return;
        }

        if (!double.IsNaN(args.NewValue))
        {
            _settings.SessionsPerLongBreak = (int)Math.Round(args.NewValue);
        }

        DispatcherQueue.TryEnqueue(Refresh);
    }

    private void OnResetToday(object sender, RoutedEventArgs e) => _timer.ResetTodaysSessions();
}
