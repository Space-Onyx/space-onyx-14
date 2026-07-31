using System.Numerics;
using Content.Shared.Bed.Sleep;
using Content.Shared.Buckle.Components;
using Content.Shared.Cuffs.Components;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Gravity;
using Content.Shared.Input;
using Content.Shared.Mobs;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._Onyx.Sprinting;

public abstract partial class SharedSprintingSystem : EntitySystem
{
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SprinterComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<SprinterComponent, SprintToggleEvent>(OnSprintToggle);
        SubscribeLocalEvent<SprinterComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<SprinterComponent, SleepStateChangedEvent>(OnSleep);
        SubscribeLocalEvent<SprinterComponent, BeforeStaminaDamageEvent>(OnBeforeStaminaDamage);
        SubscribeLocalEvent<SprinterComponent, DisarmedEvent>(OnDisarmed);
        SubscribeLocalEvent<SprinterComponent, KnockedDownEvent>(OnDisabled);
        SubscribeLocalEvent<SprinterComponent, StunnedEvent>(OnDisabled);
        SubscribeLocalEvent<SprinterComponent, DownedEvent>(OnDisabled);
        SubscribeLocalEvent<SprinterComponent, ComponentShutdown>(OnSprinterShutdown);
        SubscribeLocalEvent<StaminaComponent, ComponentRemove>(OnStaminaRemoved);
        SubscribeLocalEvent<CuffableComponent, SprintAttemptEvent>(OnCuffableAttempt);
        SubscribeLocalEvent<StandingStateComponent, SprintAttemptEvent>(OnStandingAttempt);
        SubscribeLocalEvent<BuckleComponent, SprintAttemptEvent>(OnBuckleAttempt);

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.Sprint, new SprintInputCmdHandler(this))
            .Register<SharedSprintingSystem>();
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<SharedSprintingSystem>();
        base.Shutdown();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<SprinterComponent, StaminaComponent>();
        while (query.MoveNext(out var uid, out var sprinter, out var stamina))
        {
            if (sprinter.IsSprinting)
                _stamina.TakeStaminaDamage(uid, sprinter.StaminaDrainRate * frameTime, stamina, uid, visual: false);
        }
    }

    private void OnRefreshSpeed(Entity<SprinterComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.IsSprinting)
            args.ModifySpeed(1f, ent.Comp.SprintSpeedMultiplier);
    }

    private void HandleSprintInput(ICommonSession? session, IFullInputCmdMessage message)
    {
        if (session?.AttachedEntity is not { } user)
            return;

        var uid = _mover.GetEffectiveMover(user);
        if (
            !TryComp(uid, out SprinterComponent? sprinter) ||
            !TryComp(uid, out InputMoverComponent? mover) ||
            !sprinter.IsSprinting && _mover.GetVelocityInput(mover).Sprinting == Vector2.Zero)
            return;

        if (message.State == BoundKeyState.Down && (!sprinter.CanSprint || !HasComp<StaminaComponent>(uid)))
        {
            _popup.PopupEntity(Loc.GetString("sprint-disabled"), uid, user, PopupType.Medium);
            return;
        }

        RaiseLocalEvent(uid, new SprintToggleEvent(message.State == BoundKeyState.Down));
    }

    private void OnSprintToggle(Entity<SprinterComponent> ent, ref SprintToggleEvent args) =>
        ToggleSprint(ent, ent.Comp, args.IsSprinting);

    public void ToggleSprint(EntityUid uid, SprinterComponent component, bool enabled)
    {
        if (enabled == component.IsSprinting ||
            enabled && (!component.CanSprint ||
                !HasComp<StaminaComponent>(uid) ||
                !CanSprint(uid) ||
                _timing.CurTime - component.LastSprint < component.TimeBetweenSprints))
            return;

        component.IsSprinting = enabled;
        component.LastSprint = _timing.CurTime;
        if (enabled)
        {
            RaiseLocalEvent(uid, new SprintStartEvent());
            _audio.PlayPredicted(component.SprintStartupSound, uid, uid);
        }

        _movementSpeed.RefreshMovementSpeedModifiers(uid);
        Dirty(uid, component);
    }

    private bool CanSprint(EntityUid uid)
    {
        if (_gravity.IsWeightless(uid))
        {
            _popup.PopupEntity(Loc.GetString("no-sprint-while-weightless"), uid, uid, PopupType.Medium);
            return false;
        }

        var ev = new SprintAttemptEvent();
        RaiseLocalEvent(uid, ref ev);
        return !ev.Cancelled;
    }

    private void OnCuffableAttempt(Entity<CuffableComponent> ent, ref SprintAttemptEvent args)
    {
        if (ent.Comp.CanStillInteract)
            return;

        _popup.PopupEntity(Loc.GetString("no-sprint-while-restrained"), ent, ent, PopupType.Medium);
        args.Cancel();
    }

    private void OnStandingAttempt(Entity<StandingStateComponent> ent, ref SprintAttemptEvent args)
    {
        if (!_standing.IsDown(ent.Owner))
            return;

        _popup.PopupEntity(Loc.GetString("no-sprint-while-lying"), ent, ent, PopupType.Medium);
        args.Cancel();
    }

    private static void OnBuckleAttempt(Entity<BuckleComponent> ent, ref SprintAttemptEvent args)
    {
        if (ent.Comp.BuckledTo != null)
            args.Cancel();
    }

    public void StopSprint(EntityUid uid)
    {
        if (TryComp<SprinterComponent>(uid, out var sprinter))
            ToggleSprint(uid, sprinter, false);
    }

    private void OnMobStateChanged(Entity<SprinterComponent> ent, ref MobStateChangedEvent args)
    {
        if (ent.Comp.IsSprinting && args.NewMobState is MobState.Critical or MobState.Dead)
            ToggleSprint(ent, ent.Comp, false);
    }

    private void OnSleep(Entity<SprinterComponent> ent, ref SleepStateChangedEvent args)
    {
        if (ent.Comp.IsSprinting && args.FellAsleep)
            ToggleSprint(ent, ent.Comp, false);
    }

    private static void OnBeforeStaminaDamage(Entity<SprinterComponent> ent, ref BeforeStaminaDamageEvent args)
    {
        if (ent.Comp.IsSprinting && args.Value < 0f)
            args.Value *= ent.Comp.StaminaRegenMultiplier;
    }

    private void OnDisarmed(Entity<SprinterComponent> ent, ref DisarmedEvent args)
    {
        if (!ent.Comp.IsSprinting)
            return;

        _stamina.TakeStaminaDamage(ent, ent.Comp.StaminaPenaltyOnShove, visual: false);
        ToggleSprint(ent, ent.Comp, false);
    }

    private void OnDisabled<T>(Entity<SprinterComponent> ent, ref T args) where T : notnull
    {
        if (ent.Comp.IsSprinting)
            ToggleSprint(ent, ent.Comp, false);
    }

    private void OnSprinterShutdown(Entity<SprinterComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.IsSprinting && MetaData(ent).EntityLifeStage < EntityLifeStage.Terminating)
            _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnStaminaRemoved(Entity<StaminaComponent> ent, ref ComponentRemove args)
    {
        if (TryComp<SprinterComponent>(ent, out var sprinter) && sprinter.IsSprinting)
            ToggleSprint(ent, sprinter, false);
    }

    private sealed class SprintInputCmdHandler(SharedSprintingSystem system) : InputCmdHandler
    {
        public override bool HandleCmdMessage(IEntityManager entManager, ICommonSession? session, IFullInputCmdMessage message)
        {
            system.HandleSprintInput(session, message);
            return false;
        }
    }
}
