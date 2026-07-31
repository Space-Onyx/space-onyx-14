using Content.Shared._Onyx.Shuttles.Events;
using Content.Shared.Shuttles.BUIStates;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Shuttles.UI;

public sealed partial class NavScreen
{
    private readonly ButtonGroup _dampeningButtons = new();
    public event Action<InertiaDampeningMode>? DampeningModeChanged;

    private void InitializeDampening()
    {
        DampenerAnchor.Group = _dampeningButtons;
        DampenerDampen.Group = _dampeningButtons;
        DampenerCruise.Group = _dampeningButtons;
        DampenerAnchor.OnPressed += _ => DampeningModeChanged?.Invoke(InertiaDampeningMode.Anchor);
        DampenerDampen.OnPressed += _ => DampeningModeChanged?.Invoke(InertiaDampeningMode.Dampen);
        DampenerCruise.OnPressed += _ => DampeningModeChanged?.Invoke(InertiaDampeningMode.Cruise);
        DampenerDampen.Pressed = true;
    }

    private void UpdateDampeningState(NavInterfaceState state)
    {
        DampenerAnchor.Pressed = state.DampeningMode == InertiaDampeningMode.Anchor;
        DampenerDampen.Pressed = state.DampeningMode == InertiaDampeningMode.Dampen;
        DampenerCruise.Pressed = state.DampeningMode == InertiaDampeningMode.Cruise;
        DampenerAnchor.Disabled = state.InFtl;
    }
}
