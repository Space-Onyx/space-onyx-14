using System.Numerics;
using Content.Shared._Onyx.Targeting;
using Content.Shared._Onyx.Wounds;
using Content.Shared._Onyx.Xenobiology.Slimes;
using Content.Shared.ActionBlocker;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Body;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Gibbing;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Xenobiology.Slimes;

public sealed partial class SlimeLatchSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private HungerSystem _hunger = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private WoundDamageRoutingSystem _wounds = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XenobioSlimeComponent, SlimeLatchActionEvent>(OnAction);
        SubscribeLocalEvent<XenobioSlimeComponent, SlimeLatchDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<XenobioSlimeComponent, MobStateChangedEvent>(OnSlimeStateChanged);
        SubscribeLocalEvent<XenobioSlimeComponent, PullAttemptEvent>(OnPullAttempt);
        SubscribeLocalEvent<XenobioSlimeComponent, EntGotInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<XenobioSlimeComponent, EntGotRemovedFromContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<XenobioSlimeComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<XenobioSlimeComponent, EntityTerminatingEvent>(OnSlimeTerminating);
        SubscribeLocalEvent<SlimeDigestingComponent, MobStateChangedEvent>(OnTargetStateChanged);
        SubscribeLocalEvent<SlimeDigestingComponent, PullAttemptEvent>(OnTargetPullAttempt);
        SubscribeLocalEvent<SlimeDigestingComponent, EntGotInsertedIntoContainerMessage>(OnTargetContainerChanged);
        SubscribeLocalEvent<SlimeDigestingComponent, EntGotRemovedFromContainerMessage>(OnTargetContainerChanged);
        SubscribeLocalEvent<SlimeDigestingComponent, EntityTerminatingEvent>(OnTargetTerminating);
    }

    public override void Update(float frameTime)
    {
        var latchedQuery = EntityQueryEnumerator<XenobioSlimeComponent, TransformComponent>();
        while (latchedQuery.MoveNext(out var slimeUid, out var slime, out var transform))
        {
            if (slime.LatchedTarget is not { } target || TerminatingOrDeleted(target))
            {
                Unlatch((slimeUid, slime));
                continue;
            }

            if (transform.ParentUid != target)
                _transform.SetCoordinates(slimeUid, new EntityCoordinates(target, Vector2.Zero));
            else if (transform.LocalPosition != Vector2.Zero)
                _transform.SetLocalPosition(slimeUid, Vector2.Zero, transform);
        }

        var query = EntityQueryEnumerator<SlimeDigestingComponent>();
        while (query.MoveNext(out var target, out var digesting))
        {
            if (_timing.CurTime < digesting.NextTick)
                continue;

            digesting.NextTick = _timing.CurTime + digesting.Interval;
            Digest((target, digesting));
        }
    }

    private void OnAction(Entity<XenobioSlimeComponent> slime, ref SlimeLatchActionEvent args)
    {
        if (args.Handled)
            return;

        if (slime.Comp.LatchedTarget != null)
        {
            Unlatch(slime);
            args.Handled = true;
            return;
        }

        args.Handled = TryStartLatch(slime, args.Target);
    }

    private void OnDoAfter(Entity<XenobioSlimeComponent> slime, ref SlimeLatchDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || !CanLatch(slime, target))
            return;

        args.Handled = true;
        Latch(slime, target);
        slime.Comp.LastLatchSucceeded = slime.Comp.LatchedTarget == target;
    }

    private void OnSlimeStateChanged(Entity<XenobioSlimeComponent> slime, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            Unlatch(slime);
    }

    private void OnTargetStateChanged(Entity<SlimeDigestingComponent> target, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            Unlatch(target.Comp.Slime);
    }

    private void OnTargetPullAttempt(Entity<SlimeDigestingComponent> target, ref PullAttemptEvent args)
    {
        args.Cancelled = true;
        Unlatch(target.Comp.Slime);
    }

    private void OnTargetContainerChanged(Entity<SlimeDigestingComponent> target, ref EntGotInsertedIntoContainerMessage args)
    {
        Unlatch(target.Comp.Slime);
    }

    private void OnTargetContainerChanged(Entity<SlimeDigestingComponent> target, ref EntGotRemovedFromContainerMessage args)
    {
        Unlatch(target.Comp.Slime);
    }

    private void OnPullAttempt(Entity<XenobioSlimeComponent> slime, ref PullAttemptEvent args)
    {
        if (slime.Comp.LatchedTarget == null)
            return;

        args.Cancelled = true;
        if (args.PullerUid == slime.Owner)
            return;

        Unlatch(slime);
    }

    private void OnContainerChanged(Entity<XenobioSlimeComponent> slime, ref EntGotInsertedIntoContainerMessage args)
    {
        Unlatch(slime);
    }

    private void OnContainerChanged(Entity<XenobioSlimeComponent> slime, ref EntGotRemovedFromContainerMessage args)
    {
        Unlatch(slime);
    }

    private void OnParentChanged(Entity<XenobioSlimeComponent> slime, ref EntParentChangedMessage args)
    {
        if (slime.Comp.LatchedTarget is { } target && Transform(slime).ParentUid != target)
            Unlatch(slime);
    }

    private void OnSlimeTerminating(Entity<XenobioSlimeComponent> slime, ref EntityTerminatingEvent args)
    {
        Unlatch(slime);
    }

    private void OnTargetTerminating(Entity<SlimeDigestingComponent> target, ref EntityTerminatingEvent args)
    {
        Unlatch(target.Comp.Slime);
    }

    public bool CanLatch(Entity<XenobioSlimeComponent> slime, EntityUid target)
    {
        return slime.Owner != target &&
               slime.Comp.LatchedTarget == null &&
               slime.Comp.MaxContainedEntities > 0 &&
               !TerminatingOrDeleted(target) &&
               !HasComp<XenobioSlimeComponent>(target) &&
               !HasComp<BeingLatchedComponent>(target) &&
               HasComp<BodyComponent>(target) &&
               !_mobState.IsDead(target) &&
               _actionBlocker.CanInteract(slime, target);
    }

    public bool TryStartLatch(Entity<XenobioSlimeComponent> slime, EntityUid target)
    {
        if (!CanLatch(slime, target))
        {
            var message = _mobState.IsDead(target)
                ? "xenobio-slime-latch-fail-dead"
                : HasComp<BeingLatchedComponent>(target)
                    ? "xenobio-slime-latch-fail-occupied"
                    : "xenobio-slime-latch-fail-invalid";
            _popup.PopupEntity(Loc.GetString(message, ("target", target)), slime, slime);
            return false;
        }

        var args = new DoAfterArgs(EntityManager,
            slime,
            slime.Comp.LatchDuration,
            new SlimeLatchDoAfterEvent(),
            slime,
            target: target,
            used: slime)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            BlockDuplicate = true,
            DistanceThreshold = SharedInteractionSystem.InteractionRange,
        };

        slime.Comp.LastLatchSucceeded = false;
        if (!_doAfter.TryStartDoAfter(args, out slime.Comp.LastLatchDoAfterId))
            return false;

        _popup.PopupEntity(Loc.GetString("xenobio-slime-latch-start", ("slime", slime), ("target", target)),
            slime,
            PopupType.MediumCaution);
        return true;
    }

    public bool IsLatched(Entity<XenobioSlimeComponent> slime, EntityUid target)
    {
        return slime.Comp.LatchedTarget == target;
    }

    public void Latch(Entity<XenobioSlimeComponent> slime, EntityUid target)
    {
        if (!CanLatch(slime, target))
            return;

        slime.Comp.LatchedTarget = target;
        var latched = EnsureComp<BeingLatchedComponent>(target);
        latched.Slime = slime;
        var digesting = EnsureComp<SlimeDigestingComponent>(target);
        digesting.Slime = slime;
        digesting.NextTick = _timing.CurTime + digesting.Interval;
        if (TryComp<InputMoverComponent>(slime, out var mover))
        {
            mover.CanMove = false;
            Dirty(slime.Owner, mover);
        }

        _transform.SetCoordinates(slime, new EntityCoordinates(target, Vector2.Zero));
        _audio.PlayPvs(slime.Comp.EatSound, slime);
        _popup.PopupEntity(Loc.GetString("xenobio-slime-latch-success", ("slime", slime), ("target", target)),
            slime,
            PopupType.SmallCaution);
        Dirty(slime);
    }

    public void Unlatch(EntityUid slimeUid)
    {
        if (TryComp<XenobioSlimeComponent>(slimeUid, out var slime))
            Unlatch((slimeUid, slime));
    }

    public void Unlatch(Entity<XenobioSlimeComponent> slime)
    {
        if (slime.Comp.LatchedTarget is not { } target)
            return;

        slime.Comp.LatchedTarget = null;
        if (!TerminatingOrDeleted(target))
        {
            RemComp<BeingLatchedComponent>(target);
            RemComp<SlimeDigestingComponent>(target);
            _stun.TryAddStunDuration(target, slime.Comp.OnReleaseStunDuration, visualized: true);
            _stun.TryKnockdown(target, slime.Comp.OnReleaseStunDuration, force: true);
        }

        if (TryComp<PullableComponent>(slime, out var pullable) && pullable.Puller != null)
            _pulling.TryStopPull(slime, pullable, ignoreGrab: true);

        if (TryComp<InputMoverComponent>(slime, out var mover))
        {
            mover.CanMove = true;
            Dirty(slime.Owner, mover);
        }
        if (!TerminatingOrDeleted(slime) && Transform(slime).ParentUid == target)
            _transform.AttachToGridOrMap(slime);
        Dirty(slime);
    }

    private void Digest(Entity<SlimeDigestingComponent> target)
    {
        if (!TryComp<XenobioSlimeComponent>(target.Comp.Slime, out var slime) ||
            slime.LatchedTarget != target ||
            _mobState.IsDead(target))
        {
            Unlatch(target.Comp.Slime);
            return;
        }

        _wounds.TryApplyDistributedDamage(target,
            target.Comp.Damage,
            TargetBodyPart.All,
            DamageDistribution.SplitByPartWeight,
            target.Comp.Slime,
            ignoreResistances: true);

        if (TryComp<HungerComponent>(target.Comp.Slime, out var hunger))
            _hunger.ModifyHunger(target.Comp.Slime, target.Comp.Damage.GetTotal().Float(), hunger);

        if (_mobState.IsDead(target) || !TryComp<BloodstreamComponent>(target, out var bloodstream))
            return;

        var stomachs = new List<Entity<SolutionComponent>>();
        foreach (var (organ, _) in _body.GetBodyOrgans(target.Comp.Slime))
        {
            if (!TryComp<StomachComponent>(organ, out var stomach) ||
                !_solutions.ResolveSolution(organ, StomachSystem.DefaultSolutionName, ref stomach.Solution, out var solution) ||
                solution.AvailableVolume <= FixedPoint2.Zero)
                continue;
            stomachs.Add(stomach.Solution.Value);
        }

        var sources = new List<(Entity<SolutionComponent> Solution, FixedPoint2 Volume)>();
        if (_solutions.ResolveSolution(target.Owner, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution) &&
            bloodstream.BloodSolution is { } blood)
        {
            AddSource(blood, target.Comp.ToxinReagent, sources);
            _solutions.TryAddReagent(blood, target.Comp.ToxinReagent, target.Comp.ToxinUnits, out _);
        }
        if (_solutions.ResolveSolution(target.Owner, bloodstream.MetabolitesSolutionName, ref bloodstream.MetabolitesSolution) &&
            bloodstream.MetabolitesSolution is { } metabolites)
        {
            AddSource(metabolites, target.Comp.ToxinReagent, sources);
        }
        if (_solutions.ResolveSolution(target.Owner, bloodstream.BloodTemporarySolutionName, ref bloodstream.TemporarySolution) &&
            bloodstream.TemporarySolution is { } temporary)
            AddSource(temporary, target.Comp.ToxinReagent, sources);

        var available = FixedPoint2.Zero;
        foreach (var stomach in stomachs)
            available += stomach.Comp.Solution.AvailableVolume;

        var totalSource = FixedPoint2.Zero;
        foreach (var source in sources)
            totalSource += source.Volume;

        var wanted = FixedPoint2.Min(target.Comp.SuctionUnits, available, totalSource);
        var remaining = wanted;
        for (var i = 0; i < sources.Count; i++)
        {
            var source = sources[i];
            var amount = i == sources.Count - 1
                ? remaining
                : FixedPoint2.Min(remaining, wanted * source.Volume / totalSource);
            var transferred = TransferToStomachs(source.Solution, stomachs, amount, target.Comp.ToxinReagent);
            remaining -= transferred;
        }
    }

    private FixedPoint2 TransferToStomachs(Entity<SolutionComponent> source,
        List<Entity<SolutionComponent>> stomachs,
        FixedPoint2 amount,
        Robust.Shared.Prototypes.ProtoId<Content.Shared.Chemistry.Reagent.ReagentPrototype> excluded)
    {
        var transferred = FixedPoint2.Zero;
        foreach (var stomach in stomachs)
        {
            if (amount <= FixedPoint2.Zero)
                break;

            var portion = FixedPoint2.Min(amount, stomach.Comp.Solution.AvailableVolume, source.Comp.Solution.Volume);
            if (portion <= FixedPoint2.Zero)
                continue;

            var split = _solutions.SplitSolutionWithout(source, portion, excluded);
            if (split.Volume <= FixedPoint2.Zero || !_solutions.TryAddSolution(stomach, split))
                continue;

            transferred += split.Volume;
            amount -= split.Volume;
        }
        return transferred;
    }

    private static void AddSource(Entity<SolutionComponent> source,
        Robust.Shared.Prototypes.ProtoId<Content.Shared.Chemistry.Reagent.ReagentPrototype> excluded,
        List<(Entity<SolutionComponent> Solution, FixedPoint2 Volume)> sources)
    {
        var volume = source.Comp.Solution.Volume - source.Comp.Solution.GetTotalPrototypeQuantity(excluded);
        if (volume > FixedPoint2.Zero)
            sources.Add((source, volume));
    }
}
