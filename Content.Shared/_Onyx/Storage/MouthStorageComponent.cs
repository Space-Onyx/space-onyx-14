using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Storage;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedMouthStorageSystem))]
public sealed partial class MouthStorageComponent : Component
{
    public const string MouthContainerId = "mouth";

    [DataField, AutoNetworkedField]
    public EntProtoId? OpenStorageAction;

    [DataField, AutoNetworkedField]
    public EntityUid? Action;

    [DataField]
    public EntProtoId MouthProto = "CheekStorage";

    [ViewVariables]
    public Container Mouth = default!;

    [DataField, AutoNetworkedField]
    public EntityUid? MouthId;

    [DataField]
    public FixedPoint2 SpitDamageThreshold = FixedPoint2.New(3);
}
