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
        SubscribeLocalEvent<SurgeryTreatWoundEffectComponent, SurgeryStepEvent>(OnTreatWound);
        SubscribeLocalEvent<SurgeryTreatWoundEffectComponent, SurgeryStepCompleteCheckEvent>(OnTreatWoundCheck);
    }

    private void OnHasWoundValid(Entity<SurgeryHasWoundConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (FindWound(args.Part, ent.Comp.WoundPrototype, ent.Comp.Visibility, ent.Comp.State,
                ent.Comp.Bleeding, ent.Comp.InternalBleeding) == null)
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
        var severity = GetGroupSeverity(args.Part, ent.Comp.DamageGroup);
        if (severity <= FixedPoint2.Zero || severity < ent.Comp.MinSeverity || severity > ent.Comp.MaxSeverity)
            args.Cancelled = true;
    }

    private void OnTendWounds(Entity<SurgeryTendWoundsEffectComponent> ent, ref SurgeryStepEvent args)
    {
        var severity = GetGroupSeverity(args.Part, ent.Comp.DamageGroup);
        if (severity <= FixedPoint2.Zero)
            return;

        if (!TryComp(args.Part, out DamageableComponent? damageable))
            return;

        var bonus = severity * ent.Comp.HealMultiplier * (_mobState.IsDead(args.Body) ? 0.2f : 1f);
        var current = _damage.GetPositiveDamage((args.Part, damageable));
        var adjusted = new DamageSpecifier();
        foreach (var (type, amount) in ent.Comp.Damage.Types)
            adjusted.DamageDict[type] = amount;
        foreach (var (groupId, amount) in ent.Comp.Damage.Groups)
        {
            var group = _prototypes.Index(groupId);
            var remainingTypes = group.DamageTypes.Count;
            var remainingDamage = amount;
            foreach (var type in group.DamageTypes)
            {
                var share = remainingDamage / FixedPoint2.New(remainingTypes);
                adjusted.DamageDict[type] = adjusted.DamageDict.GetValueOrDefault(type) + share;
                remainingDamage -= share;
                remainingTypes--;
            }
        }

        foreach (var type in _prototypes.Index(ent.Comp.DamageGroup).DamageTypes)
            adjusted.DamageDict[type] = adjusted.DamageDict.GetValueOrDefault(type) - bonus;

        var treatment = new DamageSpecifier();
        foreach (var (type, amount) in adjusted.DamageDict)
        {
            if (amount >= FixedPoint2.Zero)
                continue;

            treatment.DamageDict[type] = amount;
        }

        if (ent.Comp.HealDamage)
        {
            var damage = new DamageSpecifier();
            foreach (var (type, amount) in treatment.DamageDict)
            {
                var healed = FixedPoint2.Min(-amount, current.DamageDict.GetValueOrDefault(type));
                if (healed > FixedPoint2.Zero)
                    damage.DamageDict[type] = -healed;
            }

            if (!damage.Empty)
                _damageRouting.TryApplyPartDamage(args.Body, args.Part, damage, args.User, healWounds: false);
        }

        if (ent.Comp.HealWounds && !treatment.Empty)
            _wounds.TryHealWounds(args.Part, treatment);
    }

    private void OnTendWoundsCheck(Entity<SurgeryTendWoundsEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (GetGroupSeverity(args.Part, ent.Comp.DamageGroup) > FixedPoint2.Zero)
            args.Cancelled = true;
    }

    private void OnTreatWound(Entity<SurgeryTreatWoundEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (FindWound(args.Part, ent.Comp.WoundPrototype, damageGroup: ent.Comp.DamageGroup,
                internalBleeding: ent.Comp.InternalBleeding) is { } wound)
            _wounds.TreatWound(wound.Owner, ent.Comp.Amount);

        if (!ent.Comp.Damage.Empty)
            _damageRouting.TryApplyPartDamage(args.Body, args.Part, ent.Comp.Damage, args.User, healWounds: false);
    }

    private void OnTreatWoundCheck(Entity<SurgeryTreatWoundEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (FindWound(args.Part, ent.Comp.WoundPrototype, damageGroup: ent.Comp.DamageGroup,
                internalBleeding: ent.Comp.InternalBleeding) != null)
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
                !prototype.DamageTypes.Keys.Any(types.Contains))
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
        bool bleeding = false,
        bool internalBleeding = false,
        ProtoId<DamageGroupPrototype>? damageGroup = null)
    {
        if (!Resolve(part, ref part.Comp, false))
            return null;

        Entity<WoundComponent>? selected = null;
        HashSet<ProtoId<DamageTypePrototype>>? groupTypes = null;
        if (damageGroup is { } group && _prototypes.TryIndex(group, out var groupPrototype))
            groupTypes = groupPrototype.DamageTypes.ToHashSet();

        foreach (var wound in _wounds.GetWounds(part))
        {
            if (HasComp<WoundScarComponent>(wound) ||
                prototype is { } prototypeId && wound.Comp.Prototype != prototypeId ||
                state is { } woundState && wound.Comp.State != woundState)
                continue;

            if (!_prototypes.TryIndex(wound.Comp.Prototype, out var woundPrototype) ||
                groupTypes != null && !woundPrototype.DamageTypes.Keys.Any(groupTypes.Contains) ||
                visibility is { } woundVisibility && woundPrototype.Visibility != woundVisibility)
                continue;

            if (bleeding && (!TryComp(wound, out WoundBleedingComponent? bleedingComp) ||
                    wound.Comp.State != WoundState.Open || bleedingComp.CurrentRate <= 0f))
                continue;

            if (internalBleeding && (!TryComp(wound, out WoundInternalBleedingComponent? internalBleedingComp) ||
                    wound.Comp.State != WoundState.Open || internalBleedingComp.Severity <= FixedPoint2.Zero))
                continue;

            if (selected is { } current && current.Comp.Severity >= wound.Comp.Severity)
                continue;

            selected = wound;
        }

        return selected;
    }
}
