using Content.Shared.CrusherUpgrades;
using Content.Shared.Interaction;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server.CrusherUpgrades;

public sealed partial class HomingProjectileSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private RotateToFaceSystem _rotate = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<HomingProjectileComponent, TargetedProjectileComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var homing, out var targeted, out var physics))
        {
            homing.Accumulator -= frameTime;
            if (homing.Accumulator >= 0 ||
                TerminatingOrDeleted(targeted.Target) ||
                !TryComp<FixturesComponent>(uid, out var fixtures))
                continue;

            var xform = Transform(uid);
            var targetXform = Transform(targeted.Target);
            homing.Accumulator = homing.HomingTime;
            var goal = (_transform.GetMapCoordinates(targetXform).Position - _transform.GetMapCoordinates(xform).Position).ToWorldAngle();
            _rotate.TryRotateTo(uid, goal, frameTime, homing.Tolerance, MathHelper.DegreesToRadians(homing.HomingSpeed), xform);
            var velocity = _transform.GetWorldRotation(xform).ToWorldVec() * physics.LinearVelocity.Length();
            _physics.SetLinearVelocity(uid, velocity, true, true, fixtures, physics);
        }
    }
}
