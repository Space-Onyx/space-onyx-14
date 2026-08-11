using System.Linq;
using Content.Shared._Onyx.Medical.Surgery;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Medical.Surgery;

public sealed partial class WoundSurgerySystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private WoundBleedingSystem _bleeding = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private WoundDamageRoutingSystem _damageRouting = default!;
    [Dependency] private WoundFractureSystem _fractures = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private WoundSystem _wounds = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryHasWoundConditionComponent, SurgeryValidEvent>(OnHasWoundValid);
        SubscribeLocalEvent<SurgeryClampBleedingEffectComponent, SurgeryStepEvent>(OnClampBleeding);
        SubscribeLocalEvent<SurgeryClampBleedingEffectComponent, SurgeryStepCompleteCheckEvent>(OnClampBleedingCheck);
        SubscribeLocalEvent<SurgeryFractureGradeConditionComponent, SurgeryValidEvent>(OnFractureGradeValid);
        SubscribeLocalEvent<SurgeryReduceFractureEffectComponent, SurgeryStepEvent>(OnReduceFracture);
        SubscribeLocalEvent<SurgeryReduceFractureEffectComponent, SurgeryStepCompleteCheckEvent>(OnReduceFractureCheck);
        SubscribeLocalEvent<SurgeryMendFractureEffectComponent, SurgeryStepEvent>(OnMendFracture);
        SubscribeLocalEvent<SurgeryMendFractureEffectComponent, SurgeryStepCompleteCheckEvent>(OnMendFractureCheck);
        SubscribeLocalEvent<SurgeryWoundedConditionComponent, SurgeryValidEvent>(OnWoundedValid);
        SubscribeLocalEvent<SurgeryTendWoundsEffectComponent, SurgeryStepEvent>(OnTendWounds);
        SubscribeLocalEvent<SurgeryTendWoundsEffectComponent, SurgeryStepCompleteCheckEvent>(OnTendWoundsCheck);
    }

    private void OnHasWoundValid(Entity<SurgeryHasWoundConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (FindWound(args.Part, ent.Comp.WoundPrototype, ent.Comp.Visibility, ent.Comp.State, ent.Comp.Bleeding) == null)
            args.Cancelled = true;
    }

    private void OnClampBleeding(Entity<SurgeryClampBleedingEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (FindWound(args.Part, ent.Comp.WoundPrototype, bleeding: true) is { } wound)
            _bleeding.ReduceBleeding(wound.Owner, ent.Comp.Amount);
    }

    private void OnClampBleedingCheck(Entity<SurgeryClampBleedingEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (FindWound(args.Part, ent.Comp.WoundPrototype, bleeding: true) != null)
            args.Cancelled = true;
    }

    private void OnFractureGradeValid(Entity<SurgeryFractureGradeConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (_fractures.GetFracture(args.Part) is not { } fracture ||
            (ent.Comp.Grade is { } grade ? fracture.Comp2.Grade != grade : fracture.Comp2.Grade < ent.Comp.MinGrade))
            args.Cancelled = true;
    }

    private void OnReduceFracture(Entity<SurgeryReduceFractureEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (_fractures.GetFracture(args.Part) is { } fracture)
            _fractures.TryReduce(fracture.Owner);
    }

    private void OnReduceFractureCheck(Entity<SurgeryReduceFractureEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (_fractures.GetFracture(args.Part) is not { Comp2.Treatment: FractureTreatment.Reduced or FractureTreatment.Mended })
            args.Cancelled = true;
    }

    private void OnMendFracture(Entity<SurgeryMendFractureEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (_fractures.GetFracture(args.Part) is { } fracture)
            _fractures.TryMend(fracture.Owner);
    }

    private void OnMendFractureCheck(Entity<SurgeryMendFractureEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (_fractures.GetFracture(args.Part) != null)
            args.Cancelled = true;
    }

    private void OnWoundedValid(Entity<SurgeryWoundedConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (GetGroupSeverity(args.Part, ent.Comp.DamageGroup) <= FixedPoint2.Zero)
            args.Cancelled = true;
    }

    private void OnTendWounds(Entity<SurgeryTendWoundsEffectComponent> ent, ref SurgeryStepEvent args)
    {
        var severity = GetGroupSeverity(args.Part, ent.Comp.DamageGroup);
        if (severity <= FixedPoint2.Zero)
            return;

        if (!TryComp(args.Part, out DamageableComponent? damageable))
            return;

        var remaining = -ent.Comp.Damage.GetTotal();
        remaining += severity * ent.Comp.HealMultiplier * (_mobState.IsDead(args.Body) ? 0.2f : 1f);
        var current = _damage.GetPositiveDamage((args.Part, damageable));
        var damage = new DamageSpecifier();
        foreach (var type in _prototypes.Index(ent.Comp.DamageGroup).DamageTypes)
        {
            var healed = FixedPoint2.Min(remaining, current.DamageDict.GetValueOrDefault(type));
            if (healed <= FixedPoint2.Zero)
                continue;

            damage.DamageDict[type] = -healed;
            remaining -= healed;
            if (remaining <= FixedPoint2.Zero)
                break;
        }

        _damageRouting.TryApplyPartDamage(args.Body, args.Part, damage, args.User);
    }

    private void OnTendWoundsCheck(Entity<SurgeryTendWoundsEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (GetGroupSeverity(args.Part, ent.Comp.DamageGroup) > FixedPoint2.Zero)
            args.Cancelled = true;
    }

    private FixedPoint2 GetGroupSeverity(EntityUid part, ProtoId<DamageGroupPrototype> groupId)
    {
        if (!_prototypes.TryIndex(groupId, out var group))
            return FixedPoint2.Zero;

        var types = group.DamageTypes.ToHashSet();
        var severity = FixedPoint2.Zero;
        foreach (var wound in _wounds.GetWounds(part))
        {
            if (HasComp<WoundScarComponent>(wound) ||
                !_prototypes.TryIndex(wound.Comp.Prototype, out var prototype) ||
                !prototype.DamageTypes.Any(types.Contains))
                continue;

            severity += wound.Comp.Severity;
        }

        return severity;
    }

    private Entity<WoundComponent>? FindWound(
        Entity<WoundableComponent?> part,
        ProtoId<WoundPrototype>? prototype = null,
        WoundVisibility? visibility = null,
        WoundState? state = null,
        bool bleeding = false)
    {
        if (!Resolve(part, ref part.Comp, false))
            return null;

        Entity<WoundComponent>? selected = null;
        foreach (var wound in _wounds.GetWounds(part))
        {
            if (HasComp<WoundScarComponent>(wound) ||
                prototype is { } prototypeId && wound.Comp.Prototype != prototypeId ||
                state is { } woundState && wound.Comp.State != woundState ||
                visibility is { } woundVisibility &&
                (!_prototypes.TryIndex(wound.Comp.Prototype, out var woundPrototype) || woundPrototype.Visibility != woundVisibility) ||
                bleeding && (!TryComp(wound, out WoundBleedingComponent? bleedingComp) ||
                    wound.Comp.State != WoundState.Open || bleedingComp.CurrentRate <= 0f) ||
                selected is { } current && current.Comp.Severity >= wound.Comp.Severity)
                continue;

            selected = wound;
        }

        return selected;
    }
}
