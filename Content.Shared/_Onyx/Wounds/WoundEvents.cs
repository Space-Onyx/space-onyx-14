using Content.Shared.FixedPoint;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Inventory;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Wounds;

[ByRefEvent]
public readonly record struct WoundCreatedEvent(EntityUid Part, EntityUid Wound, ProtoId<WoundPrototype> Prototype);

[ByRefEvent]
public readonly record struct WoundChangedEvent(
    EntityUid Part,
    EntityUid Wound,
    FixedPoint2 OldSeverity,
    FixedPoint2 Severity);

[ByRefEvent]
public readonly record struct WoundStateChangedEvent(
    EntityUid Part,
    EntityUid Wound,
    WoundState OldState,
    WoundState State);

[ByRefEvent]
public readonly record struct WoundRemovedEvent(EntityUid Part, EntityUid Wound, ProtoId<WoundPrototype> Prototype);

[ByRefEvent]
public record struct WoundTreatmentAttemptEvent(EntityUid Part, EntityUid Wound, FixedPoint2 Amount, bool Cancelled = false);

[ByRefEvent]
public readonly record struct WoundBleedingChangedEvent(EntityUid Body, EntityUid Part, EntityUid Wound, float Rate);

[ByRefEvent]
public readonly record struct PartBleedingChangedEvent(EntityUid Body, EntityUid Part, float Rate);

[ByRefEvent]
public readonly record struct PainChangedEvent(EntityUid Entity, FixedPoint2 OldPain, FixedPoint2 Pain);

[ByRefEvent]
public readonly record struct PartDamageAppliedEvent(
    EntityUid Body,
    EntityUid Part,
    DamageSpecifier Damage,
    bool HealWounds = true);

/// <summary>
/// Raised when damage is dealt to a part that is already at (or pushed past) its
/// integrity cap. Carries the excess damage that was not applied to the part.
/// </summary>
[ByRefEvent]
public readonly record struct PartDamageOverflowedEvent(EntityUid Body, EntityUid Part, DamageSpecifier Damage);

[ByRefEvent]
public readonly record struct FractureGradeChangedEvent(
    EntityUid? Body,
    EntityUid Part,
    EntityUid Wound,
    FractureGrade OldGrade,
    FractureGrade Grade);

[ByRefEvent]
public readonly record struct FractureTreatmentChangedEvent(
    EntityUid? Body,
    EntityUid Part,
    EntityUid Wound,
    FractureTreatment OldTreatment,
    FractureTreatment Treatment);

[ByRefEvent]
public readonly record struct ScarCreatedEvent(EntityUid? Body, EntityUid Part, EntityUid Wound);

[Serializable, NetSerializable]
public enum BodyPartFunctionalityState : byte
{
    /// <summary>Part works normally.</summary>
    Functional,

    /// <summary>Part still works, but at reduced effectiveness (wounds, minor damage).</summary>
    Impaired,

    /// <summary>Part does not work at all (severe damage, untreated fracture).</summary>
    Disabled,

    /// <summary>Part is missing or detached from the body.</summary>
    Unavailable,
}

/// <summary>
/// Raised when the computed functionality of a body part changes.
/// </summary>
[ByRefEvent]
public readonly record struct BodyPartFunctionalityChangedEvent(
    EntityUid Body,
    EntityUid Part,
    BodyPartFunctionalityState OldState,
    BodyPartFunctionalityState State);

/// <summary>Raised before a do-after starts to account for the body parts manipulating the used item.</summary>
[ByRefEvent]
public record struct GetManipulationDurationMultiplierEvent(EntityUid? Used, float Multiplier = 1f);

/// <summary>
/// Public adapter contract for medical items and future explicit part targeting.
/// </summary>
[ByRefEvent]
public record struct ResolveHealingPartEvent(
    EntityUid Body,
    DamageSpecifier Healing,
    IReadOnlyList<ProtoId<DamageContainerPrototype>>? DamageContainers,
    IReadOnlySet<TreatmentCapability> TreatmentCapabilities,
    IReadOnlySet<string>? AllowedWoundStages,
    float BloodlossModifier,
    EntityUid? RequestedPart,
    bool HealWounds = true,
    EntityUid? Part = null,
    bool Accepted = false);

/// <summary>
/// Relayed to all equipped items after the struck body part is resolved.
/// Item slot and protected body part are intentionally independent.
/// </summary>
public sealed class PartDamageModifyEvent(
    EntityUid body,
    EntityUid part,
    BodyPartType partType,
    BodyPartSymmetry symmetry,
    DamageSpecifier damage) : EntityEventArgs, IInventoryRelayEvent
{
    public readonly EntityUid Body = body;
    public readonly EntityUid Part = part;
    public readonly BodyPartType PartType = partType;
    public readonly BodyPartSymmetry Symmetry = symmetry;
    public DamageSpecifier Damage = damage;
    public SlotFlags TargetSlots => SlotFlags.WITHOUT_POCKET;
}
