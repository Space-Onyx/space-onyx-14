using Content.Shared._Onyx.StationEvents;
using Content.Shared.Antag;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.StationEvents.SecretPlus;

[RegisterComponent]
public sealed partial class SecretPlusChaosComponent : Component
{
    [DataField]
    public float? ChaosScore;

    [DataField]
    public Dictionary<ProtoId<AntagSpecifierPrototype>, float> AntagChaosScores = new();
}

[RegisterComponent]
public sealed partial class SecretPlusEventComponent : Component
{
    [DataField(required: true)]
    public ProtoId<EventTypePrototype> EventType;
}
