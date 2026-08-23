using Content.Shared._Onyx.Wounds;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Medical.Surgery;

[RegisterComponent]
public sealed partial class SurgeryHasWoundConditionComponent : Component
{
    [DataField]
    public ProtoId<WoundPrototype>? WoundPrototype;

    [DataField]
    public WoundVisibility? Visibility;

    [DataField]
    public WoundState? State;

    [DataField]
    public bool Bleeding;

    [DataField]
    public bool InternalBleeding;
}

[RegisterComponent]
public sealed partial class SurgeryClampBleedingEffectComponent : Component
{
    [DataField(required: true)]
    public FixedPoint2 Amount;

    [DataField]
    public ProtoId<WoundPrototype>? WoundPrototype;
}

[RegisterComponent]
public sealed partial class SurgeryFractureGradeConditionComponent : Component
{
    [DataField]
    public FractureGrade MinGrade = FractureGrade.Hairline;

    [DataField]
    public FractureGrade? Grade;

    [DataField]
    public FractureTreatment? Treatment;
}

[RegisterComponent]
public sealed partial class SurgeryReduceFractureEffectComponent : Component;

[RegisterComponent]
public sealed partial class SurgeryMendFractureEffectComponent : Component;

[RegisterComponent]
public sealed partial class SurgeryTreatWoundEffectComponent : Component
{
    [DataField]
    public ProtoId<WoundPrototype>? WoundPrototype;

    [DataField]
    public ProtoId<DamageGroupPrototype>? DamageGroup;

    [DataField]
    public bool InternalBleeding;

    [DataField]
    public FixedPoint2 Amount = FixedPoint2.MaxValue;

    [DataField]
    public DamageSpecifier Damage = new();
}

[RegisterComponent]
public sealed partial class SurgeryWoundedConditionComponent : Component
{
    [DataField]
    public ProtoId<DamageGroupPrototype> DamageGroup = "Brute";

    [DataField]
    public FixedPoint2 MinSeverity = FixedPoint2.Zero;

    [DataField]
    public FixedPoint2 MaxSeverity = FixedPoint2.MaxValue;
}

[RegisterComponent]
public sealed partial class SurgeryTendWoundsEffectComponent : Component
{
    [DataField]
    public ProtoId<DamageGroupPrototype> DamageGroup = "Brute";

    [DataField(required: true)]
    public SurgeryTendWoundsDamage Damage = new();

    [DataField]
    public float HealMultiplier = 0.07f;

    [DataField]
    public bool HealDamage = true;

    [DataField]
    public bool HealWounds = true;
}

[DataDefinition]
public sealed partial class SurgeryTendWoundsDamage
{
    [DataField]
    public Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> Types = new();

    [DataField]
    public Dictionary<ProtoId<DamageGroupPrototype>, FixedPoint2> Groups = new();
}
