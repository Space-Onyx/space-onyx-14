using Content.Shared._Onyx.Xenobiology.Bounties;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Onyx.Xenobiology.Bounties;

[UsedImplicitly]
public sealed class XenobiologyBountyConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private XenobiologyBountyMenu? _menu;

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<XenobiologyBountyMenu>();
        _menu.OnFulfill += id => SendMessage(new XenobiologyBountyFulfillMessage(id));
        _menu.OnSkip += id => SendMessage(new XenobiologyBountySkipMessage(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is XenobiologyBountyConsoleState bountyState)
            _menu?.UpdateEntries(bountyState.Bounties, bountyState.History, bountyState.UntilNextSkip, bountyState.UntilNextRefresh);
    }
}
