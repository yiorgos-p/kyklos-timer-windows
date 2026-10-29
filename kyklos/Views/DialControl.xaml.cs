using System.Globalization;
using System.Numerics;
using Kyklos.Engine;
using Kyklos.Platform;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.System;
using Windows.UI.ViewManagement;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Kyklos.Views;

/// <summary>
/// The 60-minute dial: five-minute marks, a grey track and a coloured arc from 12 o'clock for the time left, a knob while
/// the length can be changed, and the time in the middle. Drag the ring, scroll over it, or click the time and type;
/// in mouse mode, click it to start or pause and triple-click it to reset.
/// </summary>
public sealed partial class DialControl : UserControl
{
    private const double Center = 110;

    // The ring is inset 16 from the edge and 10 wide, so its middle runs at radius 89. The minute marks sit outside it.
    private const double RingRadius = 89;
    private const double TickOuterRadius = 109;
    private const double TickLength = 6;

    /// <summary>In mouse mode, moving further than this between press and release makes it a drag rather than a click.</summary>
    private const double ClickSlop = 3;

    private TimerViewModel _timer = null!;
    private Settings _settings = null!;

    /// <summary>The minutes the dial was last dragged to, so a drag can't wrap past 12 o'clock.</summary>
    private int? _lastDragMinutes;

    private bool _dragging;
    private bool _knobHovered;
    private int _wheelDelta;
    private bool _editing;

    // Mouse mode's clicks act on release, so a drag can be told apart from them.
    private bool _clicking;
    private bool _clickDragged;
    private Point _clickPoint;
    private int _clickCount;
    private ulong _lastClickTime;

    public DialControl()
    {
        InitializeComponent();
        Ticks.Data = TicksGeometry();
        RingArea.Data = RingAreaGeometry();

        if (!new UISettings().AnimationsEnabled)
        {
            KnobDot.ScaleTransition = null;
        }
    }

    /// <summary>The timer this dial shows. The arc and the knob's dot are painted with the palette's phase colour.</summary>
    public void Attach(TimerViewModel timer, Settings settings, Palette palette)
    {
        _timer = timer;
        _settings = settings;
        Arc.Stroke = palette.Phase;
        KnobDot.Fill = palette.Phase;
    }

    internal TimerViewModel Timer => _timer;

    /// <summary>The dial and its typing field only change the length before a timer starts.</summary>
    internal bool CanEdit => _timer.State == TimerState.Stopped;

    private bool MouseMode => _settings.UseMouseControls;

    private bool CanType => CanEdit && !MouseMode;

    public void Refresh()
    {
        var state = _timer.State;
        var fraction = _timer.DialFraction;

        // Not animated: while running the arc moves a 3,600th of a turn a second, too little to see, and a drag should
        // follow the hand directly.
        Arc.Data = ArcGeometry(fraction);
        VisualStateManager.GoToState(this, state switch
        {
            TimerState.Paused => "Paused",
            TimerState.Finished => "TimesUp",
            _ => "Ready",
        }, false);

        Knob.Visibility = CanEdit ? Visibility.Visible : Visibility.Collapsed;
        var knobCenter = PointOnDial(RingRadius, fraction * 2 * Math.PI);
        KnobPosition.X = knobCenter.X - Knob.Width / 2;
        KnobPosition.Y = knobCenter.Y - Knob.Height / 2;

        // In mouse mode the whole dial takes the clicks, and dragging works from anywhere on it.
        Knob.IsHitTestVisible = !MouseMode;
        RingArea.IsHitTestVisible = CanEdit && !MouseMode;
        MouseArea.Visibility = MouseMode ? Visibility.Visible : Visibility.Collapsed;

        TimeText.Text = _timer.DisplayTime;
        CaptionText.Text = _timer.StatusCaption;
        ToolTipService.SetToolTip(TimeText, CanType ? "Click to type the minutes" : null);

        if (_editing && !CanType)
        {
            EndEditing();
        }
    }

    /// <summary>Tells screen readers that the length changed after dragging, scrolling or the arrow keys.</summary>
    private void SetMinutes(int minutes)
    {
        var before = _timer.TimerDuration;
        _timer.SetDuration(minutes);

        if (_timer.TimerDuration != before && FrameworkElementAutomationPeer.FromElement(this) is DialAutomationPeer peer)
        {
            peer.RaiseValueChanged(before, _timer.TimerDuration);
        }
    }

    // Dragging

    private void OnFacePressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(Face);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (MouseMode)
        {
            BeginClick(point);
            Face.CapturePointer(e.Pointer);
            e.Handled = true;
            return;
        }

        if (!CanEdit || !(ReferenceEquals(e.OriginalSource, RingArea) || IsWithin(e.OriginalSource as DependencyObject, Knob)))
        {
            return;
        }

        _dragging = true;
        Face.CapturePointer(e.Pointer);
        UpdateKnobDot();
        DragTo(point.Position);
        e.Handled = true;
    }

    private void OnFaceMoved(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(Face).Position;

        if (_dragging)
        {
            DragTo(position);
        }
        else if (_clicking)
        {
            // A few pixels of wobble is still a click.
            if (!_clickDragged && Distance(position, _clickPoint) > ClickSlop)
            {
                _clickDragged = true;
            }

            if (_clickDragged)
            {
                DragTo(position);
            }
        }
    }

    private void OnFaceReleased(object sender, PointerRoutedEventArgs e)
    {
        var wasClick = _clicking && !_clickDragged;
        Face.ReleasePointerCapture(e.Pointer);
        EndPointer();

        if (!wasClick)
        {
            return;
        }

        // Clicks act on release. A triple-click ends up the same as a plain reset, because the reset that follows the
        // first click's start leaves the timer stopped.
        switch (_clickCount)
        {
            case 1:
                _timer.ToggleTimer();
                break;
            case 3:
                _timer.ResetTimer();
                break;
        }
    }

    private void OnFaceCaptureLost(object sender, PointerRoutedEventArgs e) => EndPointer();

    private void EndPointer()
    {
        _dragging = false;
        _clicking = false;
        _clickDragged = false;
        _lastDragMinutes = null;
        UpdateKnobDot();
    }

    /// <summary>Turns a point on the dial into minutes, and sets the length while it can still change.</summary>
    private void DragTo(Point position)
    {
        if (!CanEdit)
        {
            return;
        }

        var minutes = Dial.MinutesAt(position.X, position.Y, Center, _lastDragMinutes);
        _lastDragMinutes = minutes;
        SetMinutes(minutes);
    }

    /// <summary>Counts clicks the way Windows counts a double-click: within its time, and close to the last one.</summary>
    private void BeginClick(PointerPoint point)
    {
        var scale = XamlRoot.RasterizationScale;
        var slopX = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXDOUBLECLK) / scale / 2;
        var slopY = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYDOUBLECLK) / scale / 2;
        var withinTime = point.Timestamp - _lastClickTime <= PInvoke.GetDoubleClickTime() * 1000UL;
        var nearby = Math.Abs(point.Position.X - _clickPoint.X) <= slopX && Math.Abs(point.Position.Y - _clickPoint.Y) <= slopY;

        _clickCount = withinTime && nearby ? _clickCount + 1 : 1;
        _lastClickTime = point.Timestamp;
        _clickPoint = point.Position;
        _clicking = true;
        _clickDragged = false;
    }

    // Scrolling

    private void OnFaceWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var properties = e.GetCurrentPoint(Face).Properties;
        if (properties.IsHorizontalMouseWheel || !CanEdit)
        {
            return;
        }

        // Moving up adds a minute, whichever way Windows is set to scroll. A mouse wheel gives 120 a notch; a precision
        // touchpad gives smaller amounts that add up to the same, so one minute takes about as far as one notch.
        var delta = properties.MouseWheelDelta;
        _wheelDelta += ScrollDirection.IsReversed(e.Pointer.PointerDeviceType) ? -delta : delta;
        while (Math.Abs(_wheelDelta) >= 120)
        {
            var step = Math.Sign(_wheelDelta);
            _wheelDelta -= step * 120;
            SetMinutes(_timer.TimerDuration + step);
        }

        e.Handled = true;
    }

    private void OnFaceExited(object sender, PointerRoutedEventArgs e) => _wheelDelta = 0;

    // The knob's dot grows a little under the pointer and shrinks while dragged, as the slider thumb's does.

    private void OnKnobEntered(object sender, PointerRoutedEventArgs e)
    {
        _knobHovered = true;
        UpdateKnobDot();
    }

    private void OnKnobExited(object sender, PointerRoutedEventArgs e)
    {
        _knobHovered = false;
        UpdateKnobDot();
    }

    private void UpdateKnobDot()
    {
        var scale = _dragging ? 0.85f : _knobHovered ? 1.15f : 1f;
        KnobDot.Scale = new Vector3(scale, scale, 1);
    }

    // Keyboard

    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        var step = e.Key switch
        {
            VirtualKey.Up or VirtualKey.Right => 1,
            VirtualKey.Down or VirtualKey.Left => -1,
            _ => 0,
        };

        if (step != 0 && CanEdit && !_editing)
        {
            SetMinutes(_timer.TimerDuration + step);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    // Typing the minutes

    private void OnTimeTapped(object sender, TappedRoutedEventArgs e)
    {
        if (CanType)
        {
            BeginEditing();
            e.Handled = true;
        }
    }

    private void BeginEditing()
    {
        _editing = true;
        MinutesBox.Text = _timer.TimerDuration.ToString(CultureInfo.InvariantCulture);
        Readout.Visibility = Visibility.Collapsed;
        Editor.Visibility = Visibility.Visible;
        MinutesBox.Focus(FocusState.Programmatic);
        MinutesBox.SelectAll();
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

    private void CommitEditing()
    {
        if (!_editing)
        {
            return;
        }

        if (int.TryParse(MinutesBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes))
        {
            SetMinutes(minutes);
        }

        EndEditing();
    }

    private void EndEditing()
    {
        _editing = false;
        Editor.Visibility = Visibility.Collapsed;
        Readout.Visibility = Visibility.Visible;
        Focus(FocusState.Programmatic);
    }

    internal bool IsEditing => _editing;

    // Geometry

    /// <summary>A point at this radius, at an angle measured clockwise from 12 o'clock.</summary>
    private static Point PointOnDial(double radius, double angle) =>
        new(Center + Math.Sin(angle) * radius, Center - Math.Cos(angle) * radius);

    private static GeometryGroup TicksGeometry()
    {
        var marks = new GeometryGroup();

        for (var mark = 0; mark < 12; mark++)
        {
            var angle = mark / 12.0 * 2 * Math.PI;
            marks.Children.Add(new LineGeometry
            {
                StartPoint = PointOnDial(TickOuterRadius, angle),
                EndPoint = PointOnDial(TickOuterRadius - TickLength, angle),
            });
        }

        return marks;
    }

    /// <summary>The band that can be dragged: the whole dial minus a middle of half its diameter.</summary>
    private static GeometryGroup RingAreaGeometry() => new()
    {
        FillRule = FillRule.EvenOdd,
        Children =
        {
            new EllipseGeometry { Center = new Point(Center, Center), RadiusX = Center, RadiusY = Center },
            new EllipseGeometry { Center = new Point(Center, Center), RadiusX = Center / 2, RadiusY = Center / 2 },
        },
    };

    /// <summary>The arc from 12 o'clock, clockwise, for this fraction of the hour.</summary>
    private static Geometry ArcGeometry(double fraction)
    {
        // An arc can't close on itself, so a full ring (time's up, or the whole hour set) is a circle.
        if (fraction >= 1)
        {
            return new EllipseGeometry { Center = new Point(Center, Center), RadiusX = RingRadius, RadiusY = RingRadius };
        }

        var figure = new PathFigure { StartPoint = PointOnDial(RingRadius, 0) };
        figure.Segments.Add(new ArcSegment
        {
            Point = PointOnDial(RingRadius, fraction * 2 * Math.PI),
            Size = new Size(RingRadius, RingRadius),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = fraction > 0.5,
        });

        return new PathGeometry { Figures = { figure } };
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static bool IsWithin(DependencyObject? element, DependencyObject container)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current == container)
            {
                return true;
            }
        }

        return false;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new DialAutomationPeer(this);
}
