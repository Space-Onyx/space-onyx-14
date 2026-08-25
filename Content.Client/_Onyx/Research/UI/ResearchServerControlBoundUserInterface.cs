using Content.Shared._Onyx.Research;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Onyx.Research.UI;

[UsedImplicitly]
public sealed class ResearchServerControlBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private ResearchServerControlWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ResearchServerControlWindow>();
        _window.OnToggleGeneration += id => SendMessage(new ToggleResearchServerGenerationMessage(id));
        _window.OnSetNetwork += (id, networkId) => SendMessage(new SetResearchServerNetworkMessage(id, networkId));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ResearchServerControlBoundInterfaceState controlState)
            _window?.UpdateState(controlState);
    }
}
