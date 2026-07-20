using System.Numerics;
using Content.Shared._Onyx.Swimming.Components;
using Content.Shared._Onyx.Swimming.Events;
using Content.Shared._Onyx.Swimming.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Ghost;
using Robust.Shared.Network;

namespace Content.Shared.Movement.Systems;

public abstract partial class SharedMoverController
{
    private const float SwimGlidePower = 0.35f;
    private const float MinStrokeInterval = 0.05f;
    private const float MinStrokeDuration = 0.01f;

    [Dependency] private INetManager _netManager = default!;

    private void ApplyOceanSwimming(
        EntityUid uid,
        InputMoverComponent mover,
        TransformComponent xform,
        ref Vector2 wishDir,
        ref float acceleration,
        ref float friction)
    {
        if (xform.MapUid is not { } mapUid ||
            !TryComp<OceanMapComponent>(mapUid, out var ocean) ||
            xform.GridUid != null ||
            HasComp<GhostComponent>(uid) ||
            HasComp<CanMoveInAirComponent>(uid))
        {
            return;
        }

        var swimming = EnsureComp<OceanSwimmingComponent>(uid);

        friction = MathF.Max(0f, ocean.WaterDrag);
        acceleration = MathF.Max(0f, ocean.StrokeAcceleration);

        if (TryComp<StaminaComponent>(uid, out var stamina))
        {
            if (!OceanSwimmingStamina.CanSwim(swimming, stamina, ocean))
            {
                if (_netManager.IsServer) {
                    swimming.NextStroke = Timing.CurTime;
                    swimming.StrokeUntil = TimeSpan.Zero;
                }
                acceleration = 0f;
                wishDir = Vector2.Zero;
                return;
            }
        }

        var wishLengthSquared = wishDir.LengthSquared();

        if (!mover.HasDirectionalMovement || wishLengthSquared <= 0f)
            return;

        var now = Timing.CurTime;

        var intervalSeconds = Math.Max(MinStrokeInterval, ocean.StrokeInterval.TotalSeconds);

        var rawDuration = ocean.StrokeDuration.TotalSeconds;
        var durationSeconds = rawDuration > intervalSeconds ? intervalSeconds : (rawDuration < MinStrokeDuration ? MinStrokeDuration : rawDuration);

        var interval = TimeSpan.FromSeconds(intervalSeconds);
        var duration = TimeSpan.FromSeconds(durationSeconds);

        if (now >= swimming.NextStroke)
        {
            if (_netManager.IsServer) {
                swimming.NextStroke = now + interval;
                swimming.StrokeUntil = now + duration;
            }
        }

        var power = GetStrokePower(now, swimming.StrokeUntil, duration);
        var speed = MathF.Max(0f, ocean.SwimSpeed);

        if (TryComp<SwimmingModifierComponent>(uid, out var modifier))
            speed *= MathF.Max(0f, modifier.SpeedMultiplier);

        var sprint = new OceanSwimmingSprintEvent();
        RaiseLocalEvent(uid, sprint);
        if (sprint.IsSprinting && ocean.SprintSpeedMultiplier > 1f)
            speed *= ocean.SprintSpeedMultiplier;

        wishDir = Vector2.Normalize(wishDir) * speed * power;
    }

    private static float GetStrokePower(TimeSpan now, TimeSpan strokeUntil, TimeSpan duration)
    {
        if (now >= strokeUntil)
            return SwimGlidePower;

        var strokeStart = strokeUntil - duration;

        var strokeProgress = Math.Clamp(
            (float) ((now - strokeStart).TotalSeconds / duration.TotalSeconds),
            0f,
            1f);

        var strokePulse = MathF.Sin(strokeProgress * MathF.PI);

        return SwimGlidePower + (1f - SwimGlidePower) * strokePulse;
    }
}