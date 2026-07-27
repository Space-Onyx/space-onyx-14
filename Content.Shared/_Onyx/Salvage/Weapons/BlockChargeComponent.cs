using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Salvage.Weapons;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlockChargeComponent : Component
{
    [DataField, AutoNetworkedField]
    public float RechargeTime = 10f;

    [DataField, AutoNetworkedField]
    public float MarkerReductionTime = 5f;

    [DataField, AutoNetworkedField]
    public TimeSpan NextCharge;

    [DataField, AutoNetworkedField]
    public bool HasCharge;
}
