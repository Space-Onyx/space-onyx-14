using Content.Server._Onyx.StationEvents.Events;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.StationEvents.Components;

[RegisterComponent, Access(typeof(RandomMultipleSpawnRule))]
public sealed partial class RandomMultipleSpawnRuleComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Prototype = string.Empty;

    [DataField]
    public int MinAmount = 1;

    [DataField]
    public int MaxAmount = 1;
}
