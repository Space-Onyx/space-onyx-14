using Content.Shared._Onyx.Swimming.Components;
using Content.Shared.Damage.Components;

namespace Content.Shared._Onyx.Swimming.Systems;

public static class OceanSwimmingStamina
{
    public static bool CanSwim(
        OceanSwimmingComponent swimming,
        StaminaComponent stamina,
        OceanMapComponent ocean)
    {
        var remaining = MathF.Max(0f, stamina.CritThreshold - stamina.StaminaDamage);
        var exhaustionThreshold = MathF.Max(0f, ocean.MinimumStaminaToSwim);
        var recoveryThreshold = MathF.Max(exhaustionThreshold, ocean.StaminaToResumeSwimming);

        if (stamina.Critical || remaining <= exhaustionThreshold)
        {
            swimming.StaminaExhausted = true;
        }
        else if (swimming.StaminaExhausted && remaining >= recoveryThreshold)
        {
            swimming.StaminaExhausted = false;
        }

        return !swimming.StaminaExhausted;
    }
}
