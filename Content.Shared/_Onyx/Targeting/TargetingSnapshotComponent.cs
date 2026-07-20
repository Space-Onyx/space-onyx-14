using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Targeting;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TargetingSnapshotComponent : Component
{
    [DataField, AutoNetworkedField]
    public TargetBodyPart RequestedTarget = TargetBodyPart.Chest;

    [DataField, AutoNetworkedField]
    public EntityUid? Shooter;
}
