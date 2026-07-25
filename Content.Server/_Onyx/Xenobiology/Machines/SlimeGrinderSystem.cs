using System.Numerics;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Power.Components;
using Content.Shared._Onyx.Xenobiology.Slimes;
using Content.Shared.Administration.Logs;
using Content.Shared.Audio;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Climbing.Events;
using Content.Shared.Construction.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Jittering;
using Content.Shared.Medical;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Power;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Onyx.Xenobiology.Machines;

public sealed partial class SlimeGrinderSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PuddleSystem _puddle = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private ThrowingSystem _throwing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeGrinderComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
        SubscribeLocalEvent<SlimeGrinderComponent, ClimbedOnEvent>(OnClimbedOn);
        SubscribeLocalEvent<SlimeGrinderComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<SlimeGrinderComponent, ReclaimerDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<ActiveSlimeGrinderComponent, ComponentInit>(OnActiveInit);
        SubscribeLocalEvent<ActiveSlimeGrinderComponent, ComponentShutdown>(OnActiveShutdown);
        SubscribeLocalEvent<ActiveSlimeGrinderComponent, UnanchorAttemptEvent>(OnUnanchorAttempt);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<SlimeGrinderComponent, ActiveSlimeGrinderComponent>();
        while (query.MoveNext(out var uid, out var grinder, out _))
        {
            grinder.ProcessingTimer -= frameTime;
            if (grinder.ProcessingTimer > 0f)
                continue;

            var unavailable = new Dictionary<EntProtoId, int>();
            foreach (var (prototype, amount) in grinder.YieldQueue)
            {
                if (!_prototypes.HasIndex<EntityPrototype>(prototype))
                {
                    Log.Error("Slime grinder {Grinder} cannot spawn missing yield prototype {Prototype}", ToPrettyString(uid), prototype);
                    unavailable[prototype] = amount;
                    continue;
                }

                for (var i = 0; i < amount; i++)
                    Spawn(prototype, Transform(uid).Coordinates);
            }

            grinder.YieldQueue.Clear();
            foreach (var (prototype, amount) in unavailable)
                grinder.YieldQueue[prototype] = amount;

            if (unavailable.Count > 0)
            {
                grinder.ProcessingTimer = 1f;
                continue;
            }

            grinder.ProcessingTimer = 0f;
            RemCompDeferred<ActiveSlimeGrinderComponent>(uid);
        }
    }

    private void OnAfterInteractUsing(Entity<SlimeGrinderComponent> grinder, ref AfterInteractUsingEvent args)
    {
        if (!args.CanReach || args.Target == null || !CanProcess(grinder, args.Used) ||
            !TryComp<PhysicsComponent>(args.Used, out var physics))
            return;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager,
            args.User,
            grinder.Comp.InsertionTimePerUnitMass * physics.FixturesMass,
            new ReclaimerDoAfterEvent(),
            grinder,
            target: grinder,
            used: args.Used)
        {
            NeedHand = true,
            BreakOnMove = true,
        });
    }

    private void OnClimbedOn(Entity<SlimeGrinderComponent> grinder, ref ClimbedOnEvent args)
    {
        if (!CanProcess(grinder, args.Climber) || !TryQueueProcess(grinder, args.Climber))
        {
            _throwing.TryThrow(args.Climber, new Vector2(_random.NextFloat(-2f, 2f), _random.NextFloat(-2f, 2f)), 0.5f);
            return;
        }

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(args.Instigator):player} ground {ToPrettyString(args.Climber):target} in {ToPrettyString(grinder):machine}");
    }

    private void OnDoAfter(Entity<SlimeGrinderComponent> grinder, ref ReclaimerDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Used is not { } slime || !CanProcess(grinder, slime) ||
            !TryQueueProcess(grinder, slime))
            return;

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(args.Args.User):player} ground {ToPrettyString(slime):target} in {ToPrettyString(grinder):machine}");
        args.Handled = true;
    }

    private bool CanProcess(Entity<SlimeGrinderComponent> grinder, EntityUid slime)
    {
        return Transform(grinder).Anchored &&
               (!TryComp<ApcPowerReceiverComponent>(grinder, out var power) || power.Powered) &&
               TryComp<XenobioSlimeComponent>(slime, out var domain) &&
               domain.ProducedExtract is { } extract &&
               _prototypes.HasIndex<EntityPrototype>(extract) &&
               domain.ExtractsProduced > 0 &&
               _mobState.IsDead(slime) &&
               HasComp<PhysicsComponent>(slime);
    }

    public bool TryQueueProcess(Entity<SlimeGrinderComponent> grinder, EntityUid slime)
    {
        if (!CanProcess(grinder, slime) ||
            !TryComp<XenobioSlimeComponent>(slime, out var domain) ||
            domain.ProducedExtract is not { } extract ||
            !TryComp<PhysicsComponent>(slime, out var physics) ||
            !TryUnloadStomachs(grinder, slime))
            return false;

        grinder.Comp.ProcessingTimer += physics.FixturesMass * grinder.Comp.ProcessingTimePerUnitMass;
        grinder.Comp.YieldQueue[extract] = grinder.Comp.YieldQueue.GetValueOrDefault(extract) + domain.ExtractsProduced;
        EnsureComp<ActiveSlimeGrinderComponent>(grinder);
        QueueDel(slime);
        return true;
    }

    private bool TryUnloadStomachs(EntityUid grinder, EntityUid slime)
    {
        var sources = new List<Entity<SolutionComponent>>();
        var contents = new Solution();
        foreach (var (organ, _) in _body.GetBodyOrgans(slime))
        {
            if (!TryComp<StomachComponent>(organ, out var stomach) ||
                !_solutions.ResolveSolution(organ, StomachSystem.DefaultSolutionName, ref stomach.Solution, out var solution) ||
                solution.Volume == 0)
                continue;
            sources.Add(stomach.Solution!.Value);
            contents.AddSolution(solution, _prototypes);
        }

        if (contents.Volume > 0 && !_puddle.TrySpillAt(grinder, contents, out _))
            return false;
        foreach (var source in sources)
            _solutions.RemoveAllSolution(source);
        return true;
    }

    private void OnPowerChanged(Entity<SlimeGrinderComponent> grinder, ref PowerChangedEvent args)
    {
        if (args.Powered && grinder.Comp.ProcessingTimer > 0f)
            EnsureComp<ActiveSlimeGrinderComponent>(grinder);
        else if (!args.Powered)
            RemComp<ActiveSlimeGrinderComponent>(grinder);
    }

    private void OnActiveInit(Entity<ActiveSlimeGrinderComponent> grinder, ref ComponentInit args)
    {
        _jitter.AddJitter(grinder, -10, 100);
        if (TryComp<SlimeGrinderComponent>(grinder, out var component))
            _audio.PlayPvs(component.GrindSound, grinder);
        _ambient.SetAmbience(grinder, true);
    }

    private void OnActiveShutdown(Entity<ActiveSlimeGrinderComponent> grinder, ref ComponentShutdown args)
    {
        RemComp<JitteringComponent>(grinder);
        _ambient.SetAmbience(grinder, false);
    }

    private void OnUnanchorAttempt(Entity<ActiveSlimeGrinderComponent> grinder, ref UnanchorAttemptEvent args)
    {
        args.Cancel();
    }
}
