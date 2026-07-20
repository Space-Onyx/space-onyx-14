using Content.Shared.Damage.Prototypes;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Wounds;

[Prototype]
public sealed partial class WoundPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public HashSet<ProtoId<DamageTypePrototype>> DamageTypes = [];

    [DataField]
    public WoundMergeMode MergeMode = WoundMergeMode.MergeByPrototype;

    [DataField]
    public FixedPoint2 MaximumSeverity = FixedPoint2.MaxValue;

    [DataField]
    public float HealingMultiplier = 1f;

    [DataField]
    public WoundVisibility Visibility = WoundVisibility.Visible;

    [DataField]
    public float BleedingRate;

    [DataField]
    public float AutomaticClottingTimeMultiplier = 1f;

    [DataField]
    public FixedPoint2? ScarThreshold;

}

[Prototype]
public sealed partial class WoundableProfilePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public ProtoId<FractureProfilePrototype>? FractureProfile;

    [DataField]
    public Dictionary<BodyPartType, ProtoId<FractureProfilePrototype>> FractureProfiles = [];

    [DataField]
    public Dictionary<BodyPartType, Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>> AmputationThresholds = [];

    [DataField]
    public Dictionary<BodyPartType, float> OrganDamageChances = new();

    [DataField]
    public Dictionary<ProtoId<OrganCategoryPrototype>, float> OrganDamageWeights = new();

    [DataField]
    public Dictionary<ProtoId<DamageTypePrototype>, float> OrganDamageMultipliers = new();
}

[Prototype]
public sealed partial class FractureProfilePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public FixedPoint2 HairlineThreshold = 8;

    [DataField]
    public FixedPoint2 SimpleThreshold = 15;

    [DataField]
    public FixedPoint2 DisplacedThreshold = 25;

    [DataField]
    public FixedPoint2 ComminutedThreshold = 40;

    [DataField]
    public float HairlineMovementModifier = 0.9f;

    [DataField]
    public float SimpleMovementModifier = 0.8f;

    [DataField]
    public float DisplacedMovementModifier = 0.6f;

    [DataField]
    public float ComminutedMovementModifier = 0.4f;

    [DataField]
    public float FootEffectScale = 0.5f;

    [DataField]
    public float HairlineManipulationModifier = 1.1f;

    [DataField]
    public float SimpleManipulationModifier = 1.25f;

    [DataField]
    public float DisplacedManipulationModifier = 1.5f;

    [DataField]
    public float ComminutedManipulationModifier = 2f;

    [DataField]
    public float HandEffectScale = 0.75f;

    [DataField]
    public float ReducedEffectScale = 0.25f;
}

[Serializable, NetSerializable]
public enum WoundMergeMode : byte
{
    MergeByPrototype,
    SeparateInstances,
}

[Serializable, NetSerializable]
public enum WoundVisibility : byte
{
    Hidden,
    Visible,
}
