using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Shared.Interaction;
using Content.Shared.Projectiles;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Onyx.Projectiles.TargetSeeking;

public sealed partial class TargetSeekingSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private RotateToFaceSystem _rotateToFace = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TargetSeekingComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    private void OnProjectileHit(Entity<TargetSeekingComponent> ent, ref ProjectileHitEvent args)
    {
        ent.Comp.CurrentTarget = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<TargetSeekingComponent, TransformComponent, PhysicsComponent, FixturesComponent>();
        while (query.MoveNext(out var uid, out var seeking, out var xform, out var body, out var fixtures))
        {
            seeking.CurrentSpeed = Math.Clamp(
                MathF.Max(seeking.CurrentSpeed, seeking.LaunchSpeed) + seeking.Acceleration * frameTime,
                0f,
                MathF.Max(0f, seeking.MaxSpeed));

            if (!TryTrackTarget(uid, seeking, xform, frameTime))
                AcquireTarget(uid, seeking, xform);

            var velocity = _transform.GetWorldRotation(xform).ToWorldVec() * seeking.CurrentSpeed;
            _physics.SetLinearVelocity(uid, velocity, manager: fixtures, body: body);
        }
    }

    public void AcquireTarget(EntityUid uid, TargetSeekingComponent component, TransformComponent xform)
    {
        component.CurrentTarget = null;
        var sourcePosition = _transform.GetMapCoordinates(uid, xform: xform).Position;
        var sourceRotation = _transform.GetWorldRotation(xform);
        var closestDistanceSquared = component.DetectionRange * component.DetectionRange;
        var scanArc = Math.Min(component.ScanArc.Degrees, component.FieldOfView);
        EntityUid? bestTarget = null;

        EntityUid? shooterGrid = null;
        if (TryComp<ProjectileComponent>(uid, out var projectile) &&
            projectile.Shooter is { } shooter &&
            TryComp(shooter, out TransformComponent? shooterXform))
        {
            shooterGrid = shooterXform.GridUid;
        }

        var query = EntityQueryEnumerator<ShuttleConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var console, out _, out var consoleXform))
        {
            if (consoleXform.MapID != xform.MapID)
                continue;

            var target = consoleXform.GridUid ?? console;
            if (target == shooterGrid || TerminatingOrDeleted(target))
                continue;

            var targetXform = Transform(target);
            var difference = _transform.GetMapCoordinates(target, xform: targetXform).Position - sourcePosition;
            var distanceSquared = difference.LengthSquared();
            if (distanceSquared > closestDistanceSquared || difference == Vector2.Zero)
                continue;

            var angleDifference = Angle.ShortestDistance(sourceRotation, difference.ToWorldAngle()).Degrees;
            if (Math.Abs(angleDifference) > scanArc / 2f)
                continue;

            closestDistanceSquared = distanceSquared;
            bestTarget = target;
        }

        component.CurrentTarget = bestTarget;
    }

    private bool TryTrackTarget(
        EntityUid uid,
        TargetSeekingComponent component,
        TransformComponent xform,
        float frameTime)
    {
        if (component.CurrentTarget is not { } target ||
            TerminatingOrDeleted(target) ||
            !TryComp(target, out TransformComponent? targetXform) ||
            targetXform.MapID != xform.MapID)
        {
            component.CurrentTarget = null;
            return false;
        }

        var sourcePosition = _transform.GetMapCoordinates(uid, xform: xform).Position;
        var targetPosition = _transform.GetMapCoordinates(target, xform: targetXform).Position;
        var difference = targetPosition - sourcePosition;
        if (difference.LengthSquared() > component.DetectionRange * component.DetectionRange)
        {
            component.CurrentTarget = null;
            return false;
        }

        var currentRotation = _transform.GetWorldRotation(xform);
        var angleDifference = Angle.ShortestDistance(currentRotation, difference.ToWorldAngle()).Degrees;
        if (Math.Abs(angleDifference) > component.ScanArc.Degrees / 2f)
        {
            component.CurrentTarget = null;
            return false;
        }

        var aimPosition = targetPosition;
        if (component.TrackingAlgorithm == TrackingMethod.Predictive && component.CurrentSpeed > 0f)
        {
            var targetVelocity = _physics.GetMapLinearVelocity(target, xform: targetXform);
            var interceptTime = GetInterceptTime(difference, targetVelocity, component.CurrentSpeed);
            if (interceptTime > 0f)
                aimPosition += targetVelocity * interceptTime;
        }

        var aimDifference = aimPosition - sourcePosition;
        if (aimDifference != Vector2.Zero)
        {
            _rotateToFace.TryRotateTo(
                uid,
                aimDifference.ToWorldAngle(),
                frameTime,
                Angle.Zero,
                component.TurnRate?.Theta ?? double.MaxValue,
                xform);
        }

        return true;
    }

    private static float GetInterceptTime(Vector2 displacement, Vector2 targetVelocity, float projectileSpeed)
    {
        var a = targetVelocity.LengthSquared() - projectileSpeed * projectileSpeed;
        var b = 2f * Vector2.Dot(displacement, targetVelocity);
        var c = displacement.LengthSquared();

        if (MathF.Abs(a) < 0.0001f)
            return MathF.Abs(b) < 0.0001f ? -1f : MathF.Max(-c / b, -1f);

        var discriminant = b * b - 4f * a * c;
        if (discriminant < 0f)
            return -1f;

        var root = MathF.Sqrt(discriminant);
        var first = (-b - root) / (2f * a);
        var second = (-b + root) / (2f * a);
        if (first > 0f && second > 0f)
            return MathF.Min(first, second);

        return MathF.Max(first, second);
    }
}
