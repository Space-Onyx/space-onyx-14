using Robust.Shared.Prototypes;
using Content.Shared.Antag;

namespace Content.Shared._Onyx.StationEvents;

[Prototype("incompatibleModes")]
public sealed partial class IncompatibleGameModesPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public HashSet<EntProtoId> Modes = new();
}

[Prototype]
public sealed partial class SecretPlusRulePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public EntProtoId Rule;

    [DataField]
    public ProtoId<EventTypePrototype>? EventType;

    [DataField]
    public float? ChaosScore;

    [DataField]
    public Dictionary<ProtoId<AntagSpecifierPrototype>, float> AntagChaosScores = new();

    [DataField]
    public bool LoneRule;
}
