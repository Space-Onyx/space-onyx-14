using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.GPS;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true, true)]
public sealed partial class GPSComponent : Component
{
    [DataField, AutoNetworkedField]
    public string GpsName = string.Empty;

    [DataField, AutoNetworkedField]
    public NetEntity? TrackedEntity;

    [DataField, AutoNetworkedField]
    public bool InDistress;

    [DataField, AutoNetworkedField]
    public bool Enabled;
}
