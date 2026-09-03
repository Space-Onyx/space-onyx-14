using Content.Server.Doors.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Emp;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.SurveillanceCamera.Components;
using Content.Shared.StationRecords;
using Content.Shared._Onyx.Cybernetics;
using Content.Shared._Onyx.Surgery.Augments;
using Content.Shared._Onyx.Surgery.Organs;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Onyx.Surgery.Augments;

public sealed partial class CyberDeckScriptEffectsSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private CyberneticsSystem _cybernetics = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private DoorSystem _doors = default!;
    [Dependency] private SharedEmpSystem _emp = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private AugmentModuleSystem _modules = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly ICollection<StationRecordKey> EmptyStationKeys = Array.Empty<StationRecordKey>();
    private const LookupFlags Lookup = LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.StaticSundries | LookupFlags.Sundries;
    private readonly HashSet<Entity<BodyComponent>> _bodies = new();
    private readonly HashSet<Entity<AirlockComponent>> _airlocks = new();
    private readonly HashSet<Entity<SurveillanceCameraComponent>> _cameras = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CyberDeckScriptImplantFailureComponent, CyberDeckScriptExecutionAttemptEvent>(OnImplantFailureAttempt);
        SubscribeLocalEvent<CyberDeckScriptImplantFailureComponent, CyberDeckScriptExecutedEvent>(OnImplantFailure);
        SubscribeLocalEvent<CyberDeckScriptRemoteDeactivationComponent, CyberDeckScriptExecutionAttemptEvent>(OnRemoteAttempt);
        SubscribeLocalEvent<CyberDeckScriptRemoteDeactivationComponent, CyberDeckScriptExecutedEvent>(OnRemoteExecuted);
        SubscribeLocalEvent<CyberDeckScriptRemoteDeactivationComponent, CyberDeckScriptDoAfterEvent>(OnRemoteDoAfter);
        SubscribeLocalEvent<CyberDeckScriptOpticsOverloadComponent, CyberDeckScriptExecutionAttemptEvent>(OnOpticsAttempt);
        SubscribeLocalEvent<CyberDeckScriptOpticsOverloadComponent, CyberDeckScriptExecutedEvent>(OnOpticsExecuted);
        SubscribeLocalEvent<CyberDeckScriptOpticsOverloadComponent, CyberDeckScriptDoAfterEvent>(OnOpticsDoAfter);
        SubscribeLocalEvent<CyberDeckScriptMotorImpairmentComponent, CyberDeckScriptExecutionAttemptEvent>(OnMotorAttempt);
        SubscribeLocalEvent<CyberDeckScriptMotorImpairmentComponent, CyberDeckScriptExecutedEvent>(OnMotorExecuted);
        SubscribeLocalEvent<CyberDeckScriptMotorImpairmentComponent, CyberDeckScriptDoAfterEvent>(OnMotorDoAfter);
    }

    private void OnImplantFailureAttempt(Entity<CyberDeckScriptImplantFailureComponent> ent, ref CyberDeckScriptExecutionAttemptEvent args)
    {
        args.Cancelled |= !float.IsFinite(ent.Comp.Range) || ent.Comp.Range <= 0f;
    }

    private void OnImplantFailure(Entity<CyberDeckScriptImplantFailureComponent> ent, ref CyberDeckScriptExecutedEvent args)
    {
        args.Handled = true;
        var duration = Duration(ent.Comp.MinDisableDuration, ent.Comp.MaxDisableDuration);
        _bodies.Clear();
        _lookup.GetEntitiesInRange(Transform(args.Body).Coordinates, ent.Comp.Range, _bodies, Lookup);
        foreach (var body in _bodies)
        {
            if (!ent.Comp.AffectSelf && body.Owner == args.Body ||
                !_interaction.InRangeUnobstructed(args.Body, body.Owner, ent.Comp.Range, CollisionGroup.Opaque))
                continue;

            var affected = false;
            foreach (var (part, _) in _body.GetBodyChildren(body))
                affected |= TryDisableCybernetic(body, part, duration, excludeEyes: true);
            foreach (var (organ, _) in _body.GetBodyOrgans(body))
            {
                if (HasComp<AugmentComponent>(organ))
                    affected |= _emp.DoEmpEffects(organ, 0f, duration, args.Performer);
                else
                    affected |= TryDisableCybernetic(body, organ, duration, excludeEyes: true);
            }
            if (!affected)
                continue;
            Spawn("EffectSparks", Transform(body).Coordinates);
            _popup.PopupEntity(Loc.GetString("cyberdeck-script-popup-implant-failure"), body, body, PopupType.LargeCaution);
        }
    }

    private void OnRemoteAttempt(Entity<CyberDeckScriptRemoteDeactivationComponent> ent, ref CyberDeckScriptExecutionAttemptEvent args)
    {
        if (!TryResolveRemote(ent.Comp, args.Target, args.Coordinates, out var target) ||
            !CanReach(args.Body, target, ent.Comp.Range, HasOptics(args.Body)))
        {
            args.Cancelled = true;
            return;
        }
        args.Target = target;
    }

    private void OnRemoteExecuted(Entity<CyberDeckScriptRemoteDeactivationComponent> ent, ref CyberDeckScriptExecutedEvent args)
    {
        if (args.TargetEntity is not { } target)
            return;
        args.Handled = StartDoAfter(ent, args, target, ent.Comp.OperationDelay);
    }

    private void OnRemoteDoAfter(Entity<CyberDeckScriptRemoteDeactivationComponent> ent, ref CyberDeckScriptDoAfterEvent args)
    {
        if (!CompleteDoAfter(ent, args, out var body, out var target) ||
            !IsRemoteTarget(target, ent.Comp) || !CanReach(body, target, ent.Comp.Range, HasOptics(body)))
            return;
        args.Handled = true;
        if (TryComp(target, out DoorComponent? door))
            _doors.TryToggleDoor(target, door);
        else if (HasComp<SurveillanceCameraComponent>(target))
            _emp.DoEmpEffects(target, 0f, Duration(ent.Comp.MinCameraDisableDuration, ent.Comp.MaxCameraDisableDuration));
    }

    private void OnOpticsAttempt(Entity<CyberDeckScriptOpticsOverloadComponent> ent, ref CyberDeckScriptExecutionAttemptEvent args)
    {
        if (!TryResolveBody(ent.Comp.TargetSearchRadius, args.Target, args.Coordinates, HasOptics, out var target))
        {
            args.Cancelled = true;
            return;
        }
        var range = HasOptics(args.Body) ? ent.Comp.Range : ent.Comp.RangeWithoutOptics;
        if (!CanReach(args.Body, target, range, HasOptics(args.Body)))
        {
            args.Cancelled = true;
            return;
        }
        args.Target = target;
    }

    private void OnOpticsExecuted(Entity<CyberDeckScriptOpticsOverloadComponent> ent, ref CyberDeckScriptExecutedEvent args)
    {
        if (args.TargetEntity is { } target)
            args.Handled = StartDoAfter(ent, args, target, ent.Comp.OperationDelay);
    }

    private void OnOpticsDoAfter(Entity<CyberDeckScriptOpticsOverloadComponent> ent, ref CyberDeckScriptDoAfterEvent args)
    {
        if (!CompleteDoAfter(ent, args, out var body, out var target) || !HasOptics(target))
            return;
        var range = HasOptics(body) ? ent.Comp.Range : ent.Comp.RangeWithoutOptics;
        if (!CanReach(body, target, range, HasOptics(body)))
            return;
        args.Handled = true;
        var affected = false;
        var duration = Duration(ent.Comp.MinDisableDuration, ent.Comp.MaxDisableDuration);
        foreach (var (organ, _) in _body.GetBodyOrgans(target))
            if (HasComp<EyesComponent>(organ))
                affected |= TryDisableCybernetic(target, organ, duration);
        if (affected)
            ShowDisruption(target, "cyberdeck-script-popup-optics-overload");
    }

    private void OnMotorAttempt(Entity<CyberDeckScriptMotorImpairmentComponent> ent, ref CyberDeckScriptExecutionAttemptEvent args)
    {
        if (!TryResolveBody(ent.Comp.TargetSearchRadius, args.Target, args.Coordinates, HasMotorics, out var target) ||
            !CanReach(args.Body, target, ent.Comp.Range))
        {
            args.Cancelled = true;
            return;
        }
        args.Target = target;
    }

    private void OnMotorExecuted(Entity<CyberDeckScriptMotorImpairmentComponent> ent, ref CyberDeckScriptExecutedEvent args)
    {
        if (args.TargetEntity is { } target)
            args.Handled = StartDoAfter(ent, args, target, ent.Comp.OperationDelay);
    }

    private void OnMotorDoAfter(Entity<CyberDeckScriptMotorImpairmentComponent> ent, ref CyberDeckScriptDoAfterEvent args)
    {
        if (!CompleteDoAfter(ent, args, out var body, out var target) || !CanReach(body, target, ent.Comp.Range))
            return;
        args.Handled = true;
        var affected = false;
        var duration = Duration(ent.Comp.MinDisableDuration, ent.Comp.MaxDisableDuration);
        foreach (var (part, partComp) in _body.GetBodyChildren(target))
        {
            if (partComp.PartType != BodyPartType.Leg)
                continue;
            affected |= TryDisableCybernetic(target, part, duration);
            foreach (var (organ, _) in _body.GetPartOrgans(part))
                affected |= HasComp<AugmentComponent>(organ)
                    ? _emp.DoEmpEffects(organ, 0f, duration, body)
                    : TryDisableCybernetic(target, organ, duration);
        }
        if (affected)
            ShowDisruption(target, "cyberdeck-script-popup-motor-impairment");
    }

    private bool TryResolveRemote(CyberDeckScriptRemoteDeactivationComponent comp, EntityUid? direct, EntityCoordinates? coordinates, out EntityUid target)
    {
        if (direct is { } entity && IsRemoteTarget(entity, comp))
        {
            target = entity;
            return true;
        }
        target = default;
        if (coordinates is not { } coords || !coords.IsValid(EntityManager))
            return false;
        var radius = NonNegative(comp.TargetSearchRadius);
        if (radius <= 0f)
            return false;
        var best = float.MaxValue;
        _airlocks.Clear();
        _lookup.GetEntitiesInRange(coords, radius, _airlocks, Lookup);
        foreach (var candidate in _airlocks)
            if (MatchesConfiguredAccess(candidate, comp))
                SelectNearest(coords, candidate, ref target, ref best);
        _cameras.Clear();
        _lookup.GetEntitiesInRange(coords, radius, _cameras, Lookup);
        foreach (var candidate in _cameras)
            if (Transform(candidate).Anchored)
                SelectNearest(coords, candidate, ref target, ref best);
        return target.IsValid();
    }

    private bool TryResolveBody(float radius, EntityUid? direct, EntityCoordinates? coordinates, Func<EntityUid, bool> predicate, out EntityUid target)
    {
        if (direct is { } entity && predicate(entity))
        {
            target = entity;
            return true;
        }
        target = default;
        if (coordinates is not { } coords || !coords.IsValid(EntityManager))
            return false;
        radius = NonNegative(radius);
        if (radius <= 0f)
            return false;
        var best = float.MaxValue;
        _bodies.Clear();
        _lookup.GetEntitiesInRange(coords, radius, _bodies, Lookup);
        foreach (var candidate in _bodies)
            if (predicate(candidate))
                SelectNearest(coords, candidate, ref target, ref best);
        return target.IsValid();
    }

    private bool StartDoAfter(EntityUid source, CyberDeckScriptExecutedEvent args, EntityUid target, float delay)
    {
        delay = Positive(delay, 0.01f);
        return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.Performer, delay,
            new CyberDeckScriptDoAfterEvent
            {
                TargetEntity = GetNetEntity(target),
                Body = GetNetEntity(args.Body),
                CyberDeck = GetNetEntity(args.CyberDeck),
            }, source, target: target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = null,
            RequireCanInteract = false,
        });
    }

    private bool CompleteDoAfter(EntityUid script, CyberDeckScriptDoAfterEvent args, out EntityUid body, out EntityUid target)
    {
        body = GetEntity(args.Body);
        target = GetEntity(args.TargetEntity);
        var deck = GetEntity(args.CyberDeck);
        return !args.Handled && !args.Cancelled && Exists(body) && Exists(target) && Exists(deck) &&
            Transform(script).ParentUid == deck && HasComp<CyberDeckComponent>(deck) && _modules.GetInstalledBody(deck) == body;
    }

    private bool TryDisableCybernetic(EntityUid body, EntityUid target, TimeSpan duration, bool excludeEyes = false)
    {
        if (excludeEyes && HasComp<EyesComponent>(target) || !TryComp(target, out CyberneticsComponent? cyber) || cyber.Disabled)
            return false;
        var protection = new CyberneticsEmpProtectionEvent(target);
        RaiseLocalEvent(body, ref protection);
        if (protection.Cancelled || protection.DurationMultiplier <= 0f)
            return false;
        return _cybernetics.TryDisable((target, cyber), TimeSpan.FromTicks((long) (duration.Ticks * protection.DurationMultiplier)));
    }

    private bool HasOptics(EntityUid body)
    {
        foreach (var (organ, _) in _body.GetBodyOrgans(body))
            if (HasComp<EyesComponent>(organ) && TryComp(organ, out CyberneticsComponent? cyber) && !cyber.Disabled)
                return true;
        return false;
    }

    private bool HasMotorics(EntityUid body)
    {
        foreach (var (part, partComp) in _body.GetBodyChildren(body))
        {
            if (partComp.PartType != BodyPartType.Leg)
                continue;
            if (TryComp(part, out CyberneticsComponent? cyber) && !cyber.Disabled)
                return true;
            foreach (var (organ, _) in _body.GetPartOrgans(part))
                if (HasComp<AugmentComponent>(organ) || TryComp(organ, out cyber) && !cyber.Disabled)
                    return true;
        }
        return false;
    }

    private bool CanReach(EntityUid user, EntityUid target, float range, bool throughWalls = false) =>
        float.IsFinite(range) && range > 0f && (throughWalls
            ? _transform.InRange(user, target, range)
            : _interaction.InRangeUnobstructed(user, target, range, CollisionGroup.Opaque));

    private bool IsRemoteTarget(EntityUid target, CyberDeckScriptRemoteDeactivationComponent comp) =>
        HasComp<AirlockComponent>(target) && HasComp<DoorComponent>(target) && MatchesConfiguredAccess(target, comp) ||
        HasComp<SurveillanceCameraComponent>(target) && Transform(target).Anchored;

    private bool MatchesConfiguredAccess(EntityUid target, CyberDeckScriptRemoteDeactivationComponent comp)
    {
        if (comp.Access.Count == 0)
            return true;

        var matches = true;
        if (_accessReader.GetMainAccessReader(target, out var readerEnt) && readerEnt is { } reader)
            matches = _accessReader.IsAllowed(comp.Access, EmptyStationKeys, reader.Owner, reader.Comp);
        return comp.Inverted ? !matches : matches;
    }

    private void SelectNearest(EntityCoordinates origin, EntityUid candidate, ref EntityUid target, ref float best)
    {
        var distance = (_transform.ToMapCoordinates(origin).Position - _transform.GetMapCoordinates(candidate).Position).LengthSquared();
        if (distance >= best)
            return;
        best = distance;
        target = candidate;
    }

    private TimeSpan Duration(float minimum, float maximum)
    {
        minimum = NonNegative(minimum);
        maximum = MathF.Max(minimum, NonNegative(maximum));
        return TimeSpan.FromSeconds(maximum > minimum ? _random.NextFloat(minimum, maximum) : minimum);
    }

    private static float NonNegative(float value) => float.IsFinite(value) ? MathF.Max(0f, value) : 0f;

    private static float Positive(float value, float fallback) => float.IsFinite(value) && value > 0f ? value : fallback;

    private void ShowDisruption(EntityUid body, string message)
    {
        Spawn("EffectSparks", Transform(body).Coordinates);
        _popup.PopupEntity(Loc.GetString(message), body, body, PopupType.LargeCaution);
    }
}
