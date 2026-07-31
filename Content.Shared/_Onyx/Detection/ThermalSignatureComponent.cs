using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Detection;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ThermalSignatureComponent : Component
{
    [DataField]
    public float StoredHeat;

    [DataField]
    public float HeatRetention = 15f / 16f;

    [ViewVariables, AutoNetworkedField]
    public float AggregatedHeat;
}
