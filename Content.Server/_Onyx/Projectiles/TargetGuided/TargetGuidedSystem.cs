using System.Numerics;
using Content.Shared.Interaction;
using Content.Shared.Projectiles;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;

namespace Content.Server._Onyx.Projectiles.TargetGuided;

public sealed partial class TargetGuidedSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private RotateToFaceSystem _rotate = default!;
    [Dependency] private PhysicsSystem _physics = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<TargetGuidedComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var guided, out var xform))
        {
            guided.Lifetime += frameTime;
            if (guided.Lifetime >= guided.MaxLifetime)
            {
                QueueDel(uid);
                continue;
            }

            guided.TimeSinceGuidance += frameTime;
            guided.CurrentSpeed = Math.Clamp(Math.Max(guided.CurrentSpeed, guided.LaunchSpeed) + guided.Acceleration * frameTime,
                guided.LaunchSpeed,
                guided.MaxSpeed);

            if (guided.FixedDirection == null && ConnectionLost(uid, guided))
                guided.FixedDirection = _transform.GetWorldRotation(xform);

            if (guided.FixedDirection is { } fixedDirection)
            {
                _transform.SetWorldRotation(xform, fixedDirection);
                _physics.SetLinearVelocity(uid, fixedDirection.ToWorldVec() * guided.CurrentSpeed);
                continue;
            }

            if (guided.TargetPosition is { } target)
            {
                var targetMap = _transform.ToMapCoordinates(target);
                var missileMap = _transform.ToMapCoordinates(xform.Coordinates);
                if (targetMap.MapId == missileMap.MapId && Vector2.DistanceSquared(targetMap.Position, missileMap.Position) > 0.01f)
                {
                    _rotate.TryRotateTo(uid,
                        Angle.FromWorldVec(targetMap.Position - missileMap.Position),
                        frameTime,
                        Angle.Zero,
                        guided.TurnRate?.Theta ?? Math.Tau,
                        xform);
                }
            }

            _physics.SetLinearVelocity(uid, _transform.GetWorldRotation(xform).ToWorldVec() * guided.CurrentSpeed);
        }
    }

    public bool SetTarget(Entity<TargetGuidedComponent> missile, EntityUid console, EntityCoordinates target)
    {
        if (missile.Comp.FixedDirection != null || !ValidConsole(console) || InFtl(missile.Owner))
            return false;

        missile.Comp.ControllingConsole = console;
        missile.Comp.TargetPosition = target;
        missile.Comp.TimeSinceGuidance = 0f;
        return true;
    }

    private bool ConnectionLost(EntityUid uid, TargetGuidedComponent guided)
    {
        return guided.TimeSinceGuidance > guided.GuidanceTimeout ||
               guided.ControllingConsole is not { } console ||
               !ValidConsole(console) ||
               InFtl(uid);
    }

    private bool ValidConsole(EntityUid console)
    {
        return Exists(console) &&
               TryComp<Content.Shared._Onyx.FireControl.FireControlConsoleComponent>(console, out var component) &&
               component.ConnectedServer != null;
    }

    private bool InFtl(EntityUid missile)
    {
        return TryComp<ProjectileComponent>(missile, out var projectile) &&
               projectile.Shooter is { } shooter &&
               TryComp(shooter, out TransformComponent? shooterXform) &&
               shooterXform.GridUid is { } grid &&
               HasComp<FTLComponent>(grid);
    }
}
