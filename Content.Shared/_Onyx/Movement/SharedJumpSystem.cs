// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Numerics;
using Content.Shared._Onyx.Sprinting;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Gravity;
using Content.Shared.Input;
using Content.Shared.Item;
using Content.Shared.LandMines;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Mousetrap;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Chasm;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Standing;
using Content.Shared.Throwing;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._Onyx.Movement;

public abstract partial class SharedJumpSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private ClimbSystem _climb = default!;
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<JumpComponent, BeforeThrowEvent>(OnBeforeThrow);
        SubscribeLocalEvent<JumpComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<JumpComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
        SubscribeLocalEvent<JumpComponent, StopThrowEvent>(OnStopThrow);
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.Jump, new JumpInputCmdHandler(this))
            .Register<SharedJumpSystem>();
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<SharedJumpSystem>();
        base.Shutdown();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<JumpComponent>();
        while (query.MoveNext(out var uid, out var jump))
        {
            if (jump.IsJumping && jump.JumpEnds <= _timing.CurTime)
                FinishJump((uid, jump));
        }
    }

    private void HandleJumpInput(ICommonSession? session, IFullInputCmdMessage message)
    {
        if (message.State != BoundKeyState.Down || session?.AttachedEntity is not { } user)
            return;

        var uid = _mover.GetEffectiveMover(user);
        if (TryComp(uid, out JumpComponent? jump))
            TryJump((uid, jump));
    }

    private void OnBeforeThrow(Entity<JumpComponent> ent, ref BeforeThrowEvent args)
    {
        if (!ent.Comp.IsJumping ||
            !TryComp(args.ItemUid, out ItemComponent? item) ||
            _item.GetSizePrototype(item.Size) > _item.GetSizePrototype("Normal"))
            return;

        args.ThrowSpeed = MathF.Max(0.1f, args.ThrowSpeed - 1f);
        args.Direction = args.Direction.Normalized() * (args.Direction.Length() + 4f);
    }

    private void OnShutdown(Entity<JumpComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.IsJumping && TryComp(ent, out ClimbingComponent? climbing))
            _climb.FinishJumpClimb((ent, climbing));
    }

    private void OnStopThrow(Entity<JumpComponent> ent, ref StopThrowEvent args)
    {
        if (args.User != ent.Owner)
            return;

        FinishJump(ent);
    }

    private void OnStepTriggerAttempt(Entity<JumpComponent> ent, ref StepTriggerAttemptEvent args)
    {
        if (ent.Comp.IsJumping &&
            args.Tripper == ent.Owner &&
            (HasComp<LandMineComponent>(args.Source) ||
             HasComp<MousetrapComponent>(args.Source) ||
             HasComp<ChasmComponent>(args.Source)))
            args.Cancelled = true;
    }

    protected virtual void OnJumpLanded(Entity<JumpComponent> ent)
    {
    }

    protected virtual void OnJumpStarted(Entity<JumpComponent> ent)
    {
    }

    public bool TryJump(Entity<JumpComponent> ent)
    {
        if (!CanJump(ent) || !TryComp(ent, out InputMoverComponent? mover))
            return false;

        var (walking, sprinting) = _mover.GetVelocityInput(mover);
        var input = walking + sprinting;

        var staminaCost = _gravity.IsWeightless((ent.Owner, null))
            ? ent.Comp.StaminaCost * ent.Comp.WeightlessStaminaCostMultiplier
            : ent.Comp.StaminaCost;
        if (!TryComp(ent, out StaminaComponent? stamina) ||
            stamina.CritThreshold - _stamina.GetStaminaDamage(ent, stamina) < ent.Comp.MinimumStamina)
        {
            _popup.PopupEntity(Loc.GetString("jump-cannot-catch-breath"), ent, ent);
            return false;
        }

        _stamina.TakeStaminaDamage(ent, staminaCost, stamina, source: ent, visual: false);

        if (input == Vector2.Zero)
        {
            ent.Comp.NextJump = _timing.CurTime + ent.Comp.Cooldown;
            ent.Comp.IsJumping = true;
            ent.Comp.MountTable = false;
            ent.Comp.JumpStarted = _timing.CurTime;
            ent.Comp.JumpEnds = _timing.CurTime + ent.Comp.StationaryDuration;
            Dirty(ent);
            OnJumpStarted(ent);
            return true;
        }

        var direction = mover.TargetRelativeRotation.RotateVec(input).Normalized();
        var xform = Transform(ent);
        var table = FindTableAhead(xform.Coordinates, direction, ent.Comp.TableDistance);

        float jumpDistance;
        var mount = false;
        if (table == null)
        {
            jumpDistance = TryComp(ent, out SprinterComponent? sprinter) && sprinter.IsSprinting
                ? ent.Comp.SprintDistance
                : ent.Comp.Distance;
        }
        else
        {
            jumpDistance = table.Value.Forward;
            mount = true;
        }

        var target = xform.Coordinates.Offset(direction * jumpDistance);

        if (TryComp(ent, out PhysicsComponent? throwPhysics))
            _physics.SetLinearVelocity(ent, Vector2.Zero, body: throwPhysics);

        if (!_throwing.TryThrow(ent, target, ent.Comp.Speed, ent, recoil: false, animated: false, playSound: false, doSpin: false))
            return false;

        if (table != null &&
            TryComp(ent, out ClimbingComponent? climbing) &&
            TryComp(table.Value.Uid, out ClimbableComponent? climbable) &&
            !climbing.IsClimbing)
        {
            _climb.StartJumpClimb((ent, climbing), (table.Value.Uid, climbable));
        }

        ent.Comp.NextJump = _timing.CurTime + ent.Comp.Cooldown;
        ent.Comp.IsJumping = true;
        ent.Comp.MountTable = mount;
        ent.Comp.JumpStarted = _timing.CurTime;
        ent.Comp.JumpEnds = _timing.CurTime + TimeSpan.FromSeconds(jumpDistance / ent.Comp.Speed);
        Dirty(ent);
        OnJumpStarted(ent);
        return true;
    }

    public bool CanJump(Entity<JumpComponent> ent)
    {
        return !ent.Comp.IsJumping &&
               ent.Comp.NextJump <= _timing.CurTime &&
               _actionBlocker.CanMove(ent) &&
               _mobState.IsAlive(ent) &&
               !_standing.IsDown((ent.Owner, null)) &&
               (!TryComp(ent, out BuckleComponent? buckle) || !buckle.Buckled) &&
               TryComp<StaminaComponent>(ent, out _) &&
               HasComp<PhysicsComponent>(ent) &&
               !HasComp<ThrownItemComponent>(ent);
    }

    private (EntityUid Uid, float Forward)? FindTableAhead(EntityCoordinates coordinates, Vector2 direction, float distance)
    {
        var origin = _transform.ToMapCoordinates(coordinates);
        EntityUid? closest = null;
        var closestForward = float.MaxValue;

        foreach (var table in _entityLookup.GetEntitiesInRange<ClimbableComponent>(coordinates, distance + 0.5f))
        {
            if (!TryComp(table, out PhysicsComponent? physics) ||
                (physics.CollisionLayer & (int) CollisionGroup.TableLayer) == 0)
                continue;

            var offset = _transform.GetMapCoordinates(table).Position - origin.Position;
            var forwardDistance = Vector2.Dot(offset, direction);
            var lateralDistance = MathF.Abs(direction.X * offset.Y - direction.Y * offset.X);
            if (forwardDistance < 0.25f || forwardDistance > distance + 0.5f || lateralDistance > 0.6f)
                continue;

            if (forwardDistance >= closestForward)
                continue;

            closest = table;
            closestForward = forwardDistance;
        }

        if (closest == null)
            return null;

        return (closest.Value, closestForward);
    }

    private bool IsOverlappingTable(EntityUid uid)
    {
        var bodyBox = _physics.GetWorldAABB(uid);
        foreach (var candidate in _entityLookup.GetEntitiesInRange<ClimbableComponent>(Transform(uid).Coordinates, 1.2f))
        {
            if (candidate.Owner == uid)
                continue;
            if (_physics.GetWorldAABB(candidate).Intersects(bodyBox))
                return true;
        }

        return false;
    }

    private void FinishJump(Entity<JumpComponent> ent)
    {
        if (!ent.Comp.IsJumping)
            return;

        ent.Comp.IsJumping = false;
        if (ent.Comp.MountTable && TryComp(ent, out PhysicsComponent? physics))
            _physics.SetLinearVelocity(ent, Vector2.Zero, wakeBody: false, body: physics);
        Dirty(ent);

        if (IsOverlappingTable(ent.Owner))
        {
            if (TryComp(ent, out ClimbingComponent? mountClimbing))
                _climb.EnsureMountedState((ent.Owner, mountClimbing));

            OnJumpLanded(ent);
            return;
        }

        if (TryComp(ent, out ClimbingComponent? climbing))
            _climb.FinishJumpClimb((ent, climbing));

        OnJumpLanded(ent);
    }

    private sealed class JumpInputCmdHandler(SharedJumpSystem system) : InputCmdHandler
    {
        public override bool HandleCmdMessage(IEntityManager entManager, ICommonSession? session, IFullInputCmdMessage message)
        {
            system.HandleJumpInput(session, message);
            return false;
        }
    }
}
