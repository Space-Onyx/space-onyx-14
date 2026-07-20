using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;

namespace Content.Shared._Onyx.Wounds;

public sealed partial class FractureMobilitySystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private WoundFractureSystem _fractures = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WoundHostComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<WoundFractureComponent, FractureGradeChangedEvent>(OnChanged);
        SubscribeLocalEvent<WoundFractureComponent, FractureTreatmentChangedEvent>(OnChanged);
        SubscribeLocalEvent<WoundFractureComponent, WoundRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<WoundableComponent, OrganGotInsertedEvent>(OnPartChanged);
        SubscribeLocalEvent<WoundableComponent, OrganGotRemovedEvent>(OnPartChanged);
    }

    private void OnRefreshSpeed(Entity<WoundHostComponent> body, ref RefreshMovementSpeedModifiersEvent args)
    {
        foreach (var (part, bodyPart) in _body.GetBodyChildren(body))
        {
            if (bodyPart.PartType is not BodyPartType.Leg and not BodyPartType.Foot ||
                !TryComp(part, out WoundableComponent? woundable) ||
                _fractures.GetFracture((part, woundable)) is not { } fracture ||
                fracture.Comp2.Treatment == FractureTreatment.Mended ||
                !_fractures.TryGetProfile((part, woundable), out var profile))
                continue;

            var modifier = GetMovementModifier(profile, fracture.Comp2.Grade);
            var partScale = bodyPart.PartType == BodyPartType.Foot ? profile.FootEffectScale : 1f;
            var treatmentScale = GetTreatmentScale(profile, fracture.Comp2.Treatment);
            args.ModifySpeed(1f - (1f - modifier) * partScale * treatmentScale);
        }
    }

    private void OnChanged(Entity<WoundFractureComponent> wound, ref FractureGradeChangedEvent args) => Refresh(args.Body);
    private void OnChanged(Entity<WoundFractureComponent> wound, ref FractureTreatmentChangedEvent args) => Refresh(args.Body);
    private void OnRemoved(Entity<WoundFractureComponent> wound, ref WoundRemovedEvent args) => RefreshPart(args.Part);
    private void OnPartChanged(Entity<WoundableComponent> part, ref OrganGotInsertedEvent args) => Refresh(args.Target);
    private void OnPartChanged(Entity<WoundableComponent> part, ref OrganGotRemovedEvent args) => Refresh(args.Target);

    private void RefreshPart(EntityUid part) => Refresh(CompOrNull<BodyPartComponent>(part)?.Body);
    private void Refresh(EntityUid? body)
    {
        if (body is { } uid)
            _movement.RefreshMovementSpeedModifiers(uid);
    }

    internal static float GetMovementModifier(FractureProfilePrototype profile, FractureGrade grade) => grade switch
    {
        FractureGrade.Hairline => profile.HairlineMovementModifier,
        FractureGrade.Simple => profile.SimpleMovementModifier,
        FractureGrade.Displaced => profile.DisplacedMovementModifier,
        FractureGrade.Comminuted => profile.ComminutedMovementModifier,
        _ => 1f,
    };

    internal static float GetTreatmentScale(FractureProfilePrototype profile, FractureTreatment treatment) => treatment switch
    {
        FractureTreatment.Reduced => profile.ReducedEffectScale,
        FractureTreatment.Mended => 0f,
        _ => 1f,
    };
}

public sealed partial class FractureManipulationSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private WoundFractureSystem _fractures = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WoundHostComponent, GetManipulationDurationMultiplierEvent>(OnGetMultiplier);
    }

    private void OnGetMultiplier(Entity<WoundHostComponent> body, ref GetManipulationDurationMultiplierEvent args)
    {
        foreach (var (part, bodyPart) in _body.GetBodyChildren(body))
        {
            if (bodyPart.PartType is not BodyPartType.Arm and not BodyPartType.Hand ||
                !TryComp(part, out WoundableComponent? woundable) ||
                _fractures.GetFracture((part, woundable)) is not { } fracture ||
                fracture.Comp2.Treatment == FractureTreatment.Mended ||
                !_fractures.TryGetProfile((part, woundable), out var profile))
                continue;

            var modifier = GetManipulationModifier(profile, fracture.Comp2.Grade);
            var partScale = bodyPart.PartType == BodyPartType.Hand ? profile.HandEffectScale : 1f;
            var treatmentScale = FractureMobilitySystem.GetTreatmentScale(profile, fracture.Comp2.Treatment);
            args.Multiplier *= 1f + (modifier - 1f) * partScale * treatmentScale;
        }
    }

    public float GetDurationMultiplier(EntityUid body)
    {
        var ev = new GetManipulationDurationMultiplierEvent(1f);
        RaiseLocalEvent(body, ref ev);
        return ev.Multiplier;
    }

    public void ApplyDoAfterPenalty(EntityUid user, DoAfterArgs args) => args.Delay *= GetDurationMultiplier(user);

    private static float GetManipulationModifier(FractureProfilePrototype profile, FractureGrade grade) => grade switch
    {
        FractureGrade.Hairline => profile.HairlineManipulationModifier,
        FractureGrade.Simple => profile.SimpleManipulationModifier,
        FractureGrade.Displaced => profile.DisplacedManipulationModifier,
        FractureGrade.Comminuted => profile.ComminutedManipulationModifier,
        _ => 1f,
    };
}
