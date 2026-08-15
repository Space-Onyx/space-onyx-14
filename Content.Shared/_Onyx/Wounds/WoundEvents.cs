using Content.Shared.FixedPoint;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Inventory;
using Content.Shared.Damage.Prototypes;
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
public readonly record struct BodyBleedingProjectionChangedEvent(EntityUid Body, float Rate);

[ByRefEvent]
public readonly record struct PainChangedEvent(EntityUid Entity, FixedPoint2 OldPain, FixedPoint2 Pain);

[ByRefEvent]
public readonly record struct PartDamageAppliedEvent(EntityUid Body, EntityUid Part, DamageSpecifier Damage);

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
public record struct FractureTreatmentAttemptEvent(
    EntityUid? Body,
    EntityUid Part,
    EntityUid Wound,
    FractureTreatment Treatment,
    bool Cancelled = false);

[ByRefEvent]
public readonly record struct FractureTreatmentChangedEvent(
    EntityUid? Body,
    EntityUid Part,
    EntityUid Wound,
    FractureTreatment OldTreatment,
    FractureTreatment Treatment);

[ByRefEvent]
public readonly record struct ScarCreatedEvent(EntityUid? Body, EntityUid Part, EntityUid Wound);

/// <summary>Raised by actions before choosing their do-after duration.</summary>
[ByRefEvent]
public record struct GetManipulationDurationMultiplierEvent(float Multiplier = 1f);

/// <summary>
/// Public adapter contract for medical items and future explicit part targeting.
/// </summary>
[ByRefEvent]
public record struct ResolveHealingPartEvent(
    EntityUid Body,
    DamageSpecifier Healing,
    IReadOnlyList<ProtoId<DamageContainerPrototype>>? DamageContainers,
    float BloodlossModifier,
    EntityUid? RequestedPart,
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
