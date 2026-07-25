using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Wires.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class ItemSlotsRequirePanelComponent : Component
{
    [DataField]
    public Dictionary<string, bool> Slots = new();
}
