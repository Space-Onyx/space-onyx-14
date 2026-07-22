using System.Numerics;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Physics.Components;

namespace Content.Shared._Onyx.Weapons;

public sealed partial class ProjectileThrowOnHitSystem : EntitySystem
{
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrowingSystem _throwing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ProjectileThrowOnHitComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<ProjectileThrowOnHitComponent, ThrowDoHitEvent>(OnThrowHit);
    }

    private void OnProjectileHit(Entity<ProjectileThrowOnHitComponent> ent, ref ProjectileHitEvent args)
    {
        if (TryComp(ent, out PhysicsComponent? physics))
            Throw(ent, ent, args.Target, physics.LinearVelocity);
    }

    private void OnThrowHit(Entity<ProjectileThrowOnHitComponent> ent, ref ThrowDoHitEvent args)
    {
        if (TryComp(args.Thrown, out PhysicsComponent? physics))
            Throw(ent, args.Component.Thrower, args.Target, physics.LinearVelocity);
    }

    private void Throw(Entity<ProjectileThrowOnHitComponent> ent, EntityUid? user, EntityUid target, Vector2 direction)
    {
        if (ent.Comp.StunTime is { } stun)
            _stun.TryUpdateParalyzeDuration(target, stun);
        if (direction == Vector2.Zero)
            return;
        _throwing.TryThrow(target,
            direction.Normalized() * ent.Comp.Distance,
            ent.Comp.Speed,
            user,
            unanchor: ent.Comp.UnanchorOnHit ? ThrowingUnanchorStrength.All : ThrowingUnanchorStrength.None);
    }
}
