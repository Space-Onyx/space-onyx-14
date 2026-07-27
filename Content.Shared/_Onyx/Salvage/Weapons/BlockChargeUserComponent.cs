using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Salvage.Weapons;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlockChargeUserComponent : Component
{
    [ViewVariables, DataField, AutoNetworkedField]
    public HashSet<EntityUid> BlockingWeapons = new();
}
