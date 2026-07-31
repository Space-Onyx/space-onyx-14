using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Holosign;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), Access(typeof(ChargeHolosignSystem))]
public sealed partial class ChargeHolosignProjectorComponent : Component
{
    [DataField(required: true)]
    public EntProtoId SignProto;

    [DataField(required: true)]
    public string SignComponentName = string.Empty;

    public Type SignComponent = default!;

    [DataField]
    public string ContainerId = "signs";

    [ViewVariables, AutoNetworkedField]
    public List<EntityUid> Signs = new();

    [ViewVariables]
    public Container Container = default!;
}
