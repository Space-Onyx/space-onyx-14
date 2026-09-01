using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Systems;

namespace Content.Shared._Onyx.Wounds;

public sealed partial class FractureEffectSystem : EntitySystem
{
    private enum EffectKind : byte
    {
        Mobility,
        Manipulation,
    }

    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private WoundFractureSystem _fractures = default!;
    [Dependency] private BodyPartFunctionalitySystem _functionality = default!;
    [Dependency] private WoundStatusEffectSystem _statusEffects = default!;
    [Dependency] private FractureAlertSystem _fractureAlerts = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WoundHostComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<WoundHostComponent, GetManipulationDurationMultiplierEvent>(OnGetMultiplier);
        SubscribeLocalEvent<WoundFractureComponent, FractureGradeChangedEvent>(OnChanged);
        SubscribeLocalEvent<WoundFractureComponent, FractureTreatmentChangedEvent>(OnChanged);
        SubscribeLocalEvent<WoundFractureComponent, WoundRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<WoundableComponent, OrganGotInsertedEvent>(OnPartChanged);
        SubscribeLocalEvent<WoundableComponent, OrganGotRemovedEvent>(OnPartChanged);
        SubscribeLocalEvent<WoundableComponent, BodyPartFunctionalityChangedEvent>(OnFunctionalityChanged);
    }
    private void OnChanged(Entity<WoundFractureComponent> wound, ref FractureGradeChangedEvent args)
    {
        Refresh(args.Body);
        _fractureAlerts.Refresh(args.Body);
        if (args.Body is { } body)
            _functionality.Refresh(body);
    }
    private void OnChanged(Entity<WoundFractureComponent> wound, ref FractureTreatmentChangedEvent args)
    {
        Refresh(args.Body);
        _fractureAlerts.Refresh(args.Body);
        if (args.Body is { } body)
            _functionality.Refresh(body);
    }
    private void OnRemoved(Entity<WoundFractureComponent> wound, ref WoundRemovedEvent args)
    {
        RefreshPart(args.Part);
        _fractureAlerts.Refresh(CompOrNull<BodyPartComponent>(args.Part)?.Body);
        if (CompOrNull<BodyPartComponent>(args.Part)?.Body is { } body)
            _functionality.Refresh(body);
    }
    private void OnPartChanged(Entity<WoundableComponent> part, ref OrganGotInsertedEvent args)
    {
        Refresh(args.Target);
        _fractureAlerts.Refresh(args.Target);
        _functionality.Refresh(args.Target);
        _statusEffects.HandlePartInserted(part.Owner);
    }
    private void OnPartChanged(Entity<WoundableComponent> part, ref OrganGotRemovedEvent args)
    {
        Refresh(args.Target);
        _fractureAlerts.Refresh(args.Target);
        _functionality.Refresh(args.Target);
        _statusEffects.HandlePartRemoved(part.Owner, args.Target);
    }

    private void OnFunctionalityChanged(Entity<WoundableComponent> part, ref BodyPartFunctionalityChangedEvent args)
    {
        Refresh(args.Body);
    }

    private void RefreshPart(EntityUid part) => Refresh(CompOrNull<BodyPartComponent>(part)?.Body);

    public void RefreshTransferredPart(EntityUid part)
    {
        var body = CompOrNull<BodyPartComponent>(part)?.Body;
        Refresh(body);
        _fractureAlerts.Refresh(body);
        if (body is { } uid)
            _functionality.RefreshPart(uid, part);
    }

    private void Refresh(EntityUid? body)
    {
        if (body is { } uid)
            _movement.RefreshMovementSpeedModifiers(uid);
    }

    private void OnRefreshSpeed(Entity<WoundHostComponent> body, ref RefreshMovementSpeedModifiersEvent args)
    {
        foreach (var (part, bodyPart) in _body.GetBodyChildren(body))
        {
            if (!body.Comp.MobilityParts.Contains(bodyPart.PartType) ||
                !TryComp(part, out WoundableComponent? woundable))
                continue;

            var (modifier, partScale, treatmentScale) = GetEffect((part, woundable), body.Comp, bodyPart, EffectKind.Mobility);
            args.ModifySpeed(1f - (1f - modifier) * partScale * treatmentScale);
        }
    }

    private void OnGetMultiplier(Entity<WoundHostComponent> body, ref GetManipulationDurationMultiplierEvent args)
    {
        if (!TryGetUsedHandSymmetry(body, args.Used, out var symmetry))
            return;

        foreach (var (part, bodyPart) in _body.GetBodyChildren(body))
        {
            if (!body.Comp.ManipulationParts.Contains(bodyPart.PartType) ||
                bodyPart.Symmetry != symmetry ||
                !TryComp(part, out WoundableComponent? woundable))
                continue;

            var (modifier, partScale, treatmentScale) = GetEffect((part, woundable), body.Comp, bodyPart, EffectKind.Manipulation);
            args.Multiplier *= 1f + (modifier - 1f) * partScale * treatmentScale;
        }
    }

    private bool TryGetUsedHandSymmetry(EntityUid body, EntityUid? used, out BodyPartSymmetry symmetry)
    {
        symmetry = BodyPartSymmetry.None;
        if (!TryComp(body, out HandsComponent? hands))
            return false;

        string? handId;
        if (used is { } item)
        {
            if (!_hands.IsHolding((body, hands), item, out handId))
                return false;
        }
        else
            handId = _hands.GetActiveHand((body, hands));

        if (!_hands.TryGetHand((body, hands), handId, out var hand))
            return false;

        symmetry = hand.Value.Location switch
        {
            HandLocation.Left or HandLocation.FunctionalLeft => BodyPartSymmetry.Left,
            HandLocation.Right or HandLocation.FunctionalRight => BodyPartSymmetry.Right,
            _ => BodyPartSymmetry.None,
        };
        return symmetry != BodyPartSymmetry.None;
    }

    private (float Modifier, float PartScale, float TreatmentScale) GetEffect(
        Entity<WoundableComponent?> part,
        WoundHostComponent host,
        BodyPartComponent bodyPart,
        EffectKind kind)
    {
        var partScale = host.PartEffectScales.GetValueOrDefault(bodyPart.PartType, 1f);

        if (_fractures.GetFracture(part) is { } fracture &&
            fracture.Comp2.Treatment != FractureTreatment.Mended &&
            _fractures.TryGetProfile(part, out var profile))
        {
            var modifier = kind switch
            {
                EffectKind.Mobility => GetGradeSettings(profile, fracture.Comp2.Grade).MovementModifier,
                _ => GetGradeSettings(profile, fracture.Comp2.Grade).ManipulationModifier,
            };
            return (modifier, partScale, GetTreatmentScale(profile, fracture.Comp2.Treatment));
        }

        var state = _functionality.GetState(part);
        var fallback = kind switch
        {
            EffectKind.Mobility => state switch
            {
                BodyPartFunctionalityState.Disabled => 0f,
                BodyPartFunctionalityState.Impaired => 0.6f,
                _ => 1f,
            },
            _ => state switch
            {
                BodyPartFunctionalityState.Disabled => 2.5f,
                BodyPartFunctionalityState.Impaired => 1.25f,
                _ => 1f,
            },
        };
        return (fallback, partScale, 1f);
    }

    public float GetDurationMultiplier(EntityUid body, EntityUid? used = null)
    {
        var ev = new GetManipulationDurationMultiplierEvent(used);
        RaiseLocalEvent(body, ref ev);
        return ev.Multiplier;
    }

    private static FractureGradeSettings GetGradeSettings(FractureProfilePrototype profile, FractureGrade grade) =>
        profile.Grades.GetValueOrDefault(grade, new FractureGradeSettings());

    private static float GetTreatmentScale(FractureProfilePrototype profile, FractureTreatment treatment) =>
        profile.TreatmentEffectScales.GetValueOrDefault(treatment, 1f);
}
