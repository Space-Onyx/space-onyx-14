using System.Numerics;
using Content.Shared.Light.Components;

namespace Content.Client.Light.EntitySystems;

public sealed partial class SunShadowSystem
{
    private static float GetCycleTime(
        SunShadowCycleComponent cycle,
        float time,
        float durationSeconds)
    {
        if (!cycle.Reverse)
            return time;

        var duration = (float) cycle.Duration.TotalSeconds;
        return time <= 0f ? 0f : duration - time;
    }

    private static void ApplyCycleSettings(
        SunShadowCycleComponent cycle,
        ref Vector2 direction,
        ref float alpha)
    {
        direction = Angle.FromDegrees(cycle.PathRotation).RotateVec(direction);

        var lengthMultiplier = float.IsFinite(cycle.LengthMultiplier)
            ? MathF.Max(0f, cycle.LengthMultiplier)
            : 1f;
        var length = MathF.Min(
            SunShadowComponent.MaxLength,
            direction.Length() * lengthMultiplier);

        direction = direction.LengthSquared() > 0f
            ? Vector2.Normalize(direction) * length
            : Vector2.Zero;

        var alphaMultiplier = float.IsFinite(cycle.AlphaMultiplier)
            ? MathF.Max(0f, cycle.AlphaMultiplier)
            : 1f;
        alpha = Math.Clamp(alpha * alphaMultiplier, 0f, 1f);
    }
}
