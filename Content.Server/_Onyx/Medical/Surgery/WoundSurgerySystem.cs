using Content.Shared._Onyx.Medical.Surgery;
using Content.Shared._Onyx.Wounds;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Medical.Surgery;

public sealed partial class WoundSurgerySystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private WoundBleedingSystem _bleeding = default!;
    [Dependency] private WoundFractureSystem _fractures = default!;
    [Dependency] private WoundSystem _wounds = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryHasWoundConditionComponent, SurgeryValidEvent>(OnHasWoundValid);
        SubscribeLocalEvent<SurgeryTreatWoundEffectComponent, SurgeryStepEvent>(OnTreatWound);
        SubscribeLocalEvent<SurgeryTreatWoundEffectComponent, SurgeryStepCompleteCheckEvent>(OnTreatWoundCheck);
        SubscribeLocalEvent<SurgeryClampBleedingEffectComponent, SurgeryStepEvent>(OnClampBleeding);
        SubscribeLocalEvent<SurgeryClampBleedingEffectComponent, SurgeryStepCompleteCheckEvent>(OnClampBleedingCheck);
        SubscribeLocalEvent<SurgeryFractureGradeConditionComponent, SurgeryValidEvent>(OnFractureGradeValid);
        SubscribeLocalEvent<SurgeryReduceFractureEffectComponent, SurgeryStepEvent>(OnReduceFracture);
        SubscribeLocalEvent<SurgeryReduceFractureEffectComponent, SurgeryStepCompleteCheckEvent>(OnReduceFractureCheck);
        SubscribeLocalEvent<SurgeryMendFractureEffectComponent, SurgeryStepEvent>(OnMendFracture);
        SubscribeLocalEvent<SurgeryMendFractureEffectComponent, SurgeryStepCompleteCheckEvent>(OnMendFractureCheck);
    }

    private void OnHasWoundValid(Entity<SurgeryHasWoundConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (FindWound(args.Part, ent.Comp.WoundPrototype, ent.Comp.Visibility, ent.Comp.State, ent.Comp.Bleeding) == null)
            args.Cancelled = true;
    }

    private void OnTreatWound(Entity<SurgeryTreatWoundEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (FindWound(args.Part, ent.Comp.WoundPrototype) is { } wound)
            _wounds.TreatWound(wound.Owner, ent.Comp.Amount);
    }

    private void OnTreatWoundCheck(Entity<SurgeryTreatWoundEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (FindWound(args.Part, ent.Comp.WoundPrototype) != null)
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
