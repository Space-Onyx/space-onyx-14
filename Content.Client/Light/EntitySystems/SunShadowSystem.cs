using System.Diagnostics.Contracts;
using System.Numerics;
using Content.Client.GameTicking.Managers;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Light.EntitySystems;

public sealed partial class SunShadowSystem : SharedSunShadowSystem
{
    [Dependency] private ClientGameTicker _ticker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MetaDataSystem _metadata = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var mapQuery = AllEntityQuery<SunShadowCycleComponent, SunShadowComponent>();
        while (mapQuery.MoveNext(out var uid,  out var cycle, out var shadow))
        {
            if (!cycle.Running || cycle.Directions.Count == 0)
                continue;

            // <Onyx-Planetar>
            var duration = cycle.Duration;
            var offset = cycle.Offset;
            var cycleTimeOwner = uid;

            LightCycleComponent? lightCycle = null;
            if (!TryComp(uid, out lightCycle))
            {
                var mapUid = Transform(uid).MapUid;
                if (mapUid is { } map && TryComp(map, out lightCycle))
                    cycleTimeOwner = map;
            }

            if (lightCycle != null)
            {
                if (!lightCycle.Running || !lightCycle.Enabled)
                {
                    shadow.Alpha = 0f;
                    continue;
                }

                duration = lightCycle.Duration;
                offset = lightCycle.Offset;
            }

            var pausedTime = _metadata.GetPauseTime(cycleTimeOwner);
            var durationSeconds = MathF.Max(1f, (float) duration.TotalSeconds);

            var time = (float)(_timing.CurTime
                .Add(offset)
                .Subtract(_ticker.RoundStartTimeSpan)
                .Subtract(pausedTime)
                .TotalSeconds % cycle.Duration.TotalSeconds);
            // <Onyx-Planetar>
            var (direction, alpha) = GetShadow((uid, cycle), GetCycleTime(cycle, time, durationSeconds));
            ApplyOnyxSettings(cycle, ref direction, ref alpha);
            // </Onyx-Planetar>
            shadow.Direction = direction;
            shadow.Alpha = alpha;
        }
    }

    [Pure]
    public (Vector2 Direction, float Alpha) GetShadow(
        Entity<SunShadowCycleComponent> entity,
        float time,
        float? durationSeconds = null)
    {
        // So essentially the values are stored as the percentages of the total duration just so it adjusts the speed
        // dynamically and we don't have to manually handle it.
        // It will lerp from each value to the next one with angle and length handled separately
        var duration = durationSeconds ?? MathF.Max(1f, (float) entity.Comp.Duration.TotalSeconds); // <Onyx-Planetar>
        var ratio = time / duration; // <Onyx-Planetar>

        for (var i = entity.Comp.Directions.Count - 1; i >= 0; i--)
        {
            var dir = entity.Comp.Directions[i];

            if (ratio >= dir.Ratio) // <Onyx-Planetar edited>
            {
                var next = entity.Comp.Directions[(i + 1) % entity.Comp.Directions.Count];
                float nextRatio;

                // Last entry
                if (i == entity.Comp.Directions.Count - 1)
                {
                    nextRatio = next.Ratio + 1f;
                }
                else
                {
                    nextRatio = next.Ratio;
                }

                var range = nextRatio - dir.Ratio;
                var diff = (ratio - dir.Ratio) / range;
                DebugTools.Assert(diff is >= 0f and <= 1f);

                // We lerp angle + length separately as we don't want a straight-line lerp and want the rotation to be consistent.
                var currentAngle = dir.Direction.ToAngle();
                var nextAngle = next.Direction.ToAngle();

                var angle = Angle.Lerp(currentAngle, nextAngle, diff);
                // This is to avoid getting weird issues where the angle gets pretty close but length still noticeably catches up.
                var lengthDiff = MathF.Pow(diff, 1f / 2f);
                var length = float.Lerp(dir.Direction.Length(), next.Direction.Length(), lengthDiff);

                var vector = angle.ToVec() * length;
                var alpha = float.Lerp(dir.Alpha, next.Alpha, diff);
                return (vector, alpha);
            }
        }

        throw new InvalidOperationException();
    }
}
