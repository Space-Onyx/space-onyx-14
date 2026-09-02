using Content.Shared._Onyx.Surgery.Augments.NeuroInterface;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Onyx.Surgery.Augments.NeuroInterface;

[UsedImplicitly]
public sealed class NeuroInterfaceBoundUserInterface : BoundUserInterface
{
    private NeuroInterfaceWindow? _window;

    public NeuroInterfaceBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<NeuroInterfaceWindow>();
        _window.OnModeChanged += mode => SendMessage(new NeuroInterfaceSetModeMessage(mode));
        _window.OnEnabledChanged += (entity, enabled) =>
            SendMessage(new NeuroInterfaceSetEnabledMessage(entity, enabled));
        _window.OnRoutingChanged += (entity, action) =>
            SendMessage(new NeuroInterfaceSetRoutingMessage(entity, action));

        if (State is NeuroInterfaceBuiState state)
            _window.UpdateState(state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is NeuroInterfaceBuiState neuroState)
            _window?.UpdateState(neuroState);
    }
}
