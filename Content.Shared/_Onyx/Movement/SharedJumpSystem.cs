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
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Throwing;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._Onyx.Movement;

public abstract partial class SharedJumpSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private ClimbSystem _climb = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
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
            if (jump.PendingJump && jump.LaunchTime <= _timing.CurTime)
            {
                if (jump.PendingDistance == 0f)
                    FinishStationaryJump((uid, jump));
                else
                    LaunchJump((uid, jump));
            }
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

    private void OnStopThrow(Entity<JumpComponent> ent, ref StopThrowEvent args)
    {
        if (args.User != ent.Owner)
            return;

        ent.Comp.IsJumping = false;
        Dirty(ent);

        if (TryComp(ent, out ClimbingComponent? climbing))
            _climb.FinishJumpClimb((ent, climbing));

        OnJumpLanded(ent);
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
        var direction = mover.HasDirectionalMovement ? walking + sprinting : Vector2.Zero;
        if (mover.HasDirectionalMovement)
        {
            direction = direction == Vector2.Zero
                ? Transform(ent).LocalRotation.ToWorldVec()
                : mover.TargetRelativeRotation.RotateVec(direction).Normalized();
        }

        var xform = Transform(ent);
        var table = direction == Vector2.Zero
            ? null
            : FindTableAhead(xform.Coordinates, direction, ent.Comp.TableDistance);
        var jumpDistance = direction == Vector2.Zero
            ? 0f
            : table != null
            ? ent.Comp.TableDistance
            : TryComp(ent, out SprinterComponent? sprinter) && sprinter.IsSprinting
                ? ent.Comp.SprintDistance
                : ent.Comp.Distance;
        var staminaCost = _gravity.IsWeightless((ent.Owner, null))
            ? ent.Comp.StaminaCost * ent.Comp.WeightlessStaminaCostMultiplier
            : ent.Comp.StaminaCost;
        var stamina = Comp<StaminaComponent>(ent);
        var remainingStamina = stamina.CritThreshold - _stamina.GetStaminaDamage(ent, stamina);
        if (remainingStamina < ent.Comp.MinimumStamina)
        {
            _popup.PopupEntity(Loc.GetString("jump-cannot-catch-breath"), ent, ent);
            return false;
        }

        _stamina.TakeStaminaDamage(ent, staminaCost, stamina, source: ent, visual: false);

        ent.Comp.NextJump = _timing.CurTime + ent.Comp.Cooldown;
        ent.Comp.PendingJump = true;
        ent.Comp.LaunchTime = _timing.CurTime + ent.Comp.Windup;
        ent.Comp.JumpDirection = direction;
        ent.Comp.PendingDistance = jumpDistance;
        ent.Comp.PendingTableJump = table != null;
        ent.Comp.IsJumping = jumpDistance == 0f;
        ent.Comp.JumpStarted = _timing.CurTime;
        ent.Comp.JumpEnds = ent.Comp.LaunchTime;
        Dirty(ent);
        OnJumpStarted(ent);
        return true;
    }

    public bool CanJump(Entity<JumpComponent> ent)
    {
        return !ent.Comp.PendingJump &&
               ent.Comp.NextJump <= _timing.CurTime &&
               _actionBlocker.CanMove(ent) &&
               _mobState.IsAlive(ent) &&
               !_standing.IsDown((ent.Owner, null)) &&
               (!TryComp(ent, out BuckleComponent? buckle) || !buckle.Buckled) &&
               TryComp<StaminaComponent>(ent, out _) &&
               HasComp<PhysicsComponent>(ent) &&
               !HasComp<ThrownItemComponent>(ent);
    }

    private void LaunchJump(Entity<JumpComponent> ent)
    {
        ent.Comp.PendingJump = false;
        var target = Transform(ent).Coordinates.Offset(ent.Comp.JumpDirection * ent.Comp.PendingDistance);
        if (!_throwing.TryThrow(ent, target, ent.Comp.Speed, ent, recoil: false, animated: false, playSound: false, doSpin: false))
        {
            Dirty(ent);
            return;
        }

        if (ent.Comp.PendingTableJump &&
            TryComp(ent, out ClimbingComponent? climbing) &&
            !climbing.IsClimbing)
        {
            _climb.StartJumpClimb((ent, climbing));
        }

        ent.Comp.IsJumping = true;
        ent.Comp.JumpStarted = _timing.CurTime;
        ent.Comp.JumpEnds = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.PendingDistance / ent.Comp.Speed);
        Dirty(ent);
    }

    private void FinishStationaryJump(Entity<JumpComponent> ent)
    {
        ent.Comp.PendingJump = false;
        ent.Comp.IsJumping = false;
        Dirty(ent);
        OnJumpLanded(ent);
    }

    private EntityUid? FindTableAhead(EntityCoordinates coordinates, Vector2 direction, float distance)
    {
        var origin = _transform.ToMapCoordinates(coordinates);
        var probe = coordinates.Offset(direction * distance / 2f);
        EntityUid? closest = null;
        var closestDistance = float.MaxValue;

        foreach (var table in _lookup.GetEntitiesInRange<ClimbableComponent>(probe, distance / 2f + 0.5f))
        {
            if (!TryComp(table, out PhysicsComponent? physics) ||
                (physics.CollisionLayer & (int) CollisionGroup.TableLayer) == 0)
                continue;

            var offset = _transform.GetMapCoordinates(table).Position - origin.Position;
            var forwardDistance = Vector2.Dot(offset, direction);
            var lateralDistance = MathF.Abs(direction.X * offset.Y - direction.Y * offset.X);
            if (forwardDistance < 0.25f || forwardDistance > distance + 0.5f || lateralDistance > 0.6f)
                continue;

            var distanceSquared = offset.LengthSquared();
            if (distanceSquared >= closestDistance)
                continue;

            closest = table;
            closestDistance = distanceSquared;
        }

        return closest;
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
