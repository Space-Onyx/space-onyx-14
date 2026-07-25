using Content.Goobstation.Shared.MartialArts;
using Content.Shared._Onyx.Grab;
using Content.Shared.ActionBlocker;
using Content.Shared.Alert;
using Content.Shared.CombatMode;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Goobstation.Shared.GrabIntent;

public sealed partial class GrabIntentSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedCombatModeSystem _combat = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private IRobustRandom _random = default!;

    private readonly SoundPathSpecifier _grabSound = new("/Audio/Effects/thudswoosh.ogg");

    public override void Initialize()
    {
        SubscribeLocalEvent<GrabbableComponent, GrabAttemptEvent>(OnGrabAttempt);
        SubscribeLocalEvent<GrabbableComponent, GrabReleaseAttemptEvent>(OnReleaseAttempt);
        SubscribeLocalEvent<GrabbableComponent, MoveInputEvent>(OnMoveInput);
        SubscribeLocalEvent<GrabbableComponent, SpeakAttemptEvent>(OnSpeakAttempt);
        SubscribeLocalEvent<GrabbableComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<GrabbableComponent, PullStoppedMessage>(OnPullStoppedGrabbable);
        SubscribeLocalEvent<GrabIntentComponent, PullStoppedMessage>(OnPullStoppedGrabber);
    }

    private void OnGrabAttempt(Entity<GrabbableComponent> ent, ref GrabAttemptEvent args)
    {
        args.Grabbed = TryAdvanceGrab(args.Puller, ent);
    }

    private void OnReleaseAttempt(Entity<GrabbableComponent> ent, ref GrabReleaseAttemptEvent args)
    {
        if (args.User == ent.Owner && ent.Comp.GrabStage != GrabStage.No)
        {
            TryResist(ent);
            args.Released = ent.Comp.GrabStage == GrabStage.No;
        }
    }

    private bool TryAdvanceGrab(EntityUid pullerUid, Entity<GrabbableComponent> target)
    {
        if (!_combat.IsInCombatMode(pullerUid)
            || !HasComp<MobStateComponent>(target)
            || !TryComp<PullerComponent>(pullerUid, out var puller)
            || puller.Pulling != target.Owner
            || !TryComp<PullableComponent>(target, out var pullable)
            || pullable.Puller != pullerUid
            || !TryComp<GrabIntentComponent>(pullerUid, out var grabber))
            return false;

        if (_timing.CurTime < grabber.NextStageChange)
            return true;

        var stage = (GrabStage) Math.Min((int) GrabStage.Suffocate, (int) grabber.GrabStage + 1);
        grabber.NextStageChange = _timing.CurTime + grabber.StageChangeCooldown;
        SetStage((pullerUid, puller, grabber), (target.Owner, pullable, target.Comp), stage);
        RaiseLocalEvent(pullerUid,
            new ComboAttackPerformedEvent(pullerUid, target.Owner, pullerUid, ComboAttackType.Grab));
        return true;
    }

    private void SetStage(
        Entity<PullerComponent, GrabIntentComponent> puller,
        Entity<PullableComponent, GrabbableComponent> target,
        GrabStage stage)
    {
        var modifier = new RaiseGrabModifierEvent(puller.Owner, stage);
        RaiseLocalEvent(ref modifier);
        stage = modifier.NewStage ?? stage;
        puller.Comp2.GrabStage = stage;
        target.Comp2.GrabStage = stage;
        _alerts.ShowAlert(puller.Owner, puller.Comp1.PullingAlert, (short) stage);
        _alerts.ShowAlert(target.Owner, target.Comp1.PulledAlert, (short) stage);
        _blocker.UpdateCanMove(target);
        Dirty(puller, puller.Comp2);
        Dirty(target, target.Comp2);

        var name = stage.ToString().ToLowerInvariant();
        _popup.PopupEntity(
            Loc.GetString($"popup-grab-{name}-self", ("target", Identity.Entity(target, EntityManager))),
            Loc.GetString($"popup-grab-{name}-others", ("target", Identity.Entity(target, EntityManager)), ("puller", Identity.Entity(puller, EntityManager))),
            target,
            puller,
            PopupType.Medium);
        _popup.PopupEntity(
            Loc.GetString($"popup-grab-{name}-target", ("puller", Identity.Entity(puller, EntityManager))),
            null,
            target,
            target,
            stage == GrabStage.Suffocate ? PopupType.LargeCaution : PopupType.MediumCaution);
        _audio.PlayPredicted(_grabSound, target, puller);
    }

    private void OnMoveInput(Entity<GrabbableComponent> ent, ref MoveInputEvent args)
    {
        if (!args.HasDirectionalMovement || ent.Comp.GrabStage >= GrabStage.Hard)
            return;
        TryResist(ent);
    }

    private void OnSpeakAttempt(Entity<GrabbableComponent> ent, ref SpeakAttemptEvent args)
    {
        if (ent.Comp.GrabStage == GrabStage.Suffocate)
            args.Cancel();
    }

    private void OnUpdateCanMove(Entity<GrabbableComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.GrabStage >= GrabStage.Hard)
            args.Cancel();
    }

    public bool TryResist(Entity<GrabbableComponent> target)
    {
        if (!TryComp<PullableComponent>(target, out var pullable)
            || pullable.Puller is not { } pullerUid
            || !TryComp<PullerComponent>(pullerUid, out var puller)
            || !TryComp<GrabIntentComponent>(pullerUid, out var grabber)
            || _timing.CurTime < target.Comp.NextEscapeAttempt)
            return false;

        target.Comp.NextEscapeAttempt = _timing.CurTime + target.Comp.EscapeAttemptCooldown;
        Dirty(target, target.Comp);
        var chance = grabber.EscapeChances.GetValueOrDefault(target.Comp.GrabStage, 1f) * MassRatio(target, pullerUid);
        if (!_random.Prob(Math.Clamp(chance, 0.05f, 1f)))
        {
            _popup.PopupEntity(Loc.GetString("popup-grab-release-fail-self"), target, target);
            return false;
        }

        if (target.Comp.GrabStage <= GrabStage.Soft)
            return _pulling.TryStopPull(target, pullable, target, ignoreGrab: true);

        SetStage((pullerUid, puller, grabber), (target.Owner, pullable, target.Comp), target.Comp.GrabStage - 1);
        return true;
    }

    private float MassRatio(EntityUid target, EntityUid puller)
    {
        if (!TryComp<PhysicsComponent>(target, out var targetPhysics)
            || !TryComp<PhysicsComponent>(puller, out var pullerPhysics)
            || targetPhysics.InvMass <= 0f
            || pullerPhysics.InvMass <= 0f)
            return 1f;
        return Math.Clamp(pullerPhysics.InvMass / targetPhysics.InvMass, 0.5f, 2f);
    }

    private void OnPullStoppedGrabbable(Entity<GrabbableComponent> ent, ref PullStoppedMessage args)
    {
        if (args.PulledUid != ent.Owner)
            return;
        ent.Comp.GrabStage = GrabStage.No;
        _blocker.UpdateCanMove(ent);
        Dirty(ent);
    }

    private void OnPullStoppedGrabber(Entity<GrabIntentComponent> ent, ref PullStoppedMessage args)
    {
        if (args.PullerUid != ent.Owner)
            return;
        ent.Comp.GrabStage = GrabStage.No;
        Dirty(ent);
    }
}
