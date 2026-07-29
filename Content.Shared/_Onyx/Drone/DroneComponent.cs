using Content.Shared.Alert;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Drone;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause, AutoGenerateComponentState]
public sealed partial class DroneComponent : Component
{
    [DataField]
    public float InteractionBlockRange = 1.5f;

    [DataField]
    public TimeSpan ProximityDelay = TimeSpan.FromSeconds(2);

    [AutoPausedField]
    public TimeSpan NextProximityAlert;

    public EntityUid NearestEntity;

    [DataField, AutoNetworkedField]
    public EntityWhitelist? Whitelist;

    [DataField, AutoNetworkedField]
    public EntityWhitelist? Blacklist;

    [DataField]
    public ProtoId<AlertPrototype> BatteryAlert = "DroneBattery";

    [DataField]
    public ProtoId<AlertPrototype> NoBatteryAlert = "BorgBatteryNone";

    public short LastChargePercent;

    [AutoPausedField]
    public TimeSpan NextBatteryUpdate;
}
