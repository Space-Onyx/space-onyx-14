using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared.Movement.Components;

public sealed partial class FloorOccluderComponent
{
    [DataField, AutoNetworkedField]
    public ProtoId<TagPrototype>? IgnoreWhenOnTileWithTag;
}
