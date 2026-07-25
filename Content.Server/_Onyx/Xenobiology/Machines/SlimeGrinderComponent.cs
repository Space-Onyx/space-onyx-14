using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Xenobiology.Machines;

[RegisterComponent]
public sealed partial class SlimeGrinderComponent : Component
{
    [ViewVariables]
    public float ProcessingTimer;

    [ViewVariables]
    public Dictionary<EntProtoId, int> YieldQueue = new();

    [DataField]
    public float InsertionTimePerUnitMass = 0.1f;

    [DataField]
    public float ProcessingTimePerUnitMass = 0.1f;

    [DataField]
    public SoundSpecifier GrindSound = new SoundPathSpecifier("/Audio/Machines/reclaimer_startup.ogg");
}

[RegisterComponent]
public sealed partial class ActiveSlimeGrinderComponent : Component;
