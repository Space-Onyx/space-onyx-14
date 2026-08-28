using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Onyx.Chemistry.Circulation;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CirculatoryStreamComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextUpdate;

    [AutoNetworkedField]
    public Dictionary<ProtoId<CirculatoryStreamPrototype>, float> BleedRates = new();

    [AutoNetworkedField]
    public HashSet<ProtoId<CirculatoryStreamPrototype>> InitializedStreams = new();
}
