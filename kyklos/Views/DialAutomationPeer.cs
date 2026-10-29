using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;

namespace Kyklos.Views;

/// <summary>
/// The dial for screen readers: one adjustable element named "Timer". Its value reads like "25:00, ready", and its range is
/// the length in minutes, 1 to 60, which the arrow keys change while the timer is stopped.
/// </summary>
internal sealed partial class DialAutomationPeer(DialControl owner)
    : FrameworkElementAutomationPeer(owner), IRangeValueProvider, IValueProvider
{
    private DialControl Dial => (DialControl)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;

    protected override string GetClassNameCore() => nameof(DialControl);

    protected override string GetNameCore() => "Timer";

    protected override object? GetPatternCore(PatternInterface patternInterface) =>
        patternInterface is PatternInterface.RangeValue or PatternInterface.Value ? this : base.GetPatternCore(patternInterface);

    // The time and the caption are read as the dial's value, so they aren't separate elements, except the field for
    // typing the minutes while it's open.
    protected override IList<AutomationPeer>? GetChildrenCore() => Dial.IsEditing ? base.GetChildrenCore() : null;

    public void RaiseValueChanged(int before, int after)
    {
        RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty, (double)before, (double)after);
        RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, "", Text);
    }

    private string Text => $"{Dial.Timer.DisplayTime}, {Dial.Timer.StatusCaption}";

    // The length in minutes

    double IRangeValueProvider.Value => Dial.Timer.TimerDuration;

    bool IRangeValueProvider.IsReadOnly => !Dial.CanEdit;

    public double Minimum => 1;

    public double Maximum => Engine.TimerViewModel.MaximumMinutes;

    public double SmallChange => 1;

    public double LargeChange => 5;

    void IRangeValueProvider.SetValue(double value) => Dial.Timer.SetDuration((int)Math.Round(value));

    // What's read out

    string IValueProvider.Value => Text;

    bool IValueProvider.IsReadOnly => true;

    void IValueProvider.SetValue(string value) => throw new InvalidOperationException("The timer's text can't be set.");
}
