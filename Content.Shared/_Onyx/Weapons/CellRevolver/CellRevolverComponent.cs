using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Weapons.CellRevolver;

[RegisterComponent, NetworkedComponent]
public sealed partial class CellRevolverComponent : Component
{
    [DataField] public string CellSlot = "cell_slot";
}
