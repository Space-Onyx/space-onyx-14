using Content.Shared.Atmos;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Atmos.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GasDestroyerComponent : Component
{
    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public GasDestroyerState DestroyerState = GasDestroyerState.Disabled;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MinExternalAmount;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MinExternalPressure;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public Gas? DestroyGas;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public Dictionary<Gas, float>? ListDestroyGas;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool DestroyAnyGas;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float DestroyAmount = Atmospherics.MolesCellStandard * 20f;
}

[Serializable, NetSerializable]
public enum GasDestroyerState : byte
{
    Disabled,
    Idle,
    Working,
}
