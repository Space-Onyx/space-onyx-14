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
}

[RegisterComponent]
public sealed partial class SurgeryReduceFractureEffectComponent : Component;

[RegisterComponent]
public sealed partial class SurgeryMendFractureEffectComponent : Component;

[RegisterComponent]
public sealed partial class SurgeryWoundedConditionComponent : Component
{
    [DataField]
    public ProtoId<DamageGroupPrototype> DamageGroup = "Brute";
}

[RegisterComponent]
public sealed partial class SurgeryTendWoundsEffectComponent : Component
{
    [DataField]
    public ProtoId<DamageGroupPrototype> DamageGroup = "Brute";

    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    [DataField]
    public float HealMultiplier = 0.07f;
}
