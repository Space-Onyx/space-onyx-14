using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Harvestable;

[RegisterComponent]
public sealed partial class HarvestableComponent : Component
{
    [DataField(required: true)]
    public EntProtoId? Loot;

    [DataField]
    public float Delay = 1f;
}
