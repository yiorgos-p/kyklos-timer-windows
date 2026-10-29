using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;

namespace Kyklos.Views;

/// <summary>
/// The toolkit's segmented control, made readable to screen readers. Segmented has no automation peer of its own, so it
/// appeared as a plain group: nothing said which phase was selected, and a phase couldn't be picked.
/// </summary>
public sealed partial class PhaseSwitcher : Segmented
{
    public PhaseSwitcher()
    {
        DefaultStyleKey = typeof(Segmented);

        // Tell a screen reader when the phase changes.
        SelectionChanged += (_, e) =>
        {
            foreach (var item in e.AddedItems.OfType<UIElement>())
            {
                FrameworkElementAutomationPeer.FromElement(item)?.RaiseAutomationEvent(AutomationEvents.SelectionItemPatternOnElementSelected);
            }
        };
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SwitcherPeer(this);

    private sealed partial class SwitcherPeer(PhaseSwitcher owner) : FrameworkElementAutomationPeer(owner), ISelectionProvider
    {
        private PhaseSwitcher Switcher => (PhaseSwitcher)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override string GetClassNameCore() => nameof(PhaseSwitcher);

        protected override object? GetPatternCore(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.Selection ? this : base.GetPatternCore(patternInterface);

        public bool CanSelectMultiple => false;

        public bool IsSelectionRequired => true;

        public IRawElementProviderSimple[] GetSelection() =>
            Switcher.ContainerFromIndex(Switcher.SelectedIndex) is UIElement item && CreatePeerForElement(item) is { } peer
                ? [ProviderFromPeer(peer)]
                : [];
    }
}

/// <summary>A phase in the switcher, which says whether it's selected and can be selected by a screen reader.</summary>
public sealed partial class PhaseItem : SegmentedItem
{
    public PhaseItem()
    {
        DefaultStyleKey = typeof(SegmentedItem);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ItemPeer(this);

    private sealed partial class ItemPeer(PhaseItem owner) : FrameworkElementAutomationPeer(owner), ISelectionItemProvider
    {
        private PhaseItem Item => (PhaseItem)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override string GetClassNameCore() => nameof(PhaseItem);

        protected override string GetNameCore() => Item.Content as string ?? base.GetNameCore();

        protected override object? GetPatternCore(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.SelectionItem ? this : base.GetPatternCore(patternInterface);

        public bool IsSelected => Item.IsSelected;

        public IRawElementProviderSimple? SelectionContainer =>
            ItemsControl.ItemsControlFromItemContainer(Item) is { } switcher && CreatePeerForElement(switcher) is { } peer
                ? ProviderFromPeer(peer)
                : null;

        public void Select() => Item.IsSelected = true;

        public void AddToSelection() => Select();

        // One phase is always selected; choosing another is how this one stops being.
        public void RemoveFromSelection()
        {
        }
    }
}
