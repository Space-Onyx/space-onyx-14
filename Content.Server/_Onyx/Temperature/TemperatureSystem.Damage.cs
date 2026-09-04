using Content.Shared.Damage;
using Content.Shared.Temperature.Components;
using Content.Shared._Onyx.Targeting;
using Content.Shared._Onyx.Wounds;
using Content.Shared.StatusEffectNew;

namespace Content.Server.Temperature.Systems;

public sealed partial class TemperatureSystem
{
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    [Dependency] private WoundDamageRoutingSystem _woundDamageRouting = default!;

    private partial bool TryApplyLocalizedTemperatureDamage(Entity<TemperatureDamageComponent> entity, DamageSpecifier damage);

    private partial bool TryApplyLocalizedTemperatureDamage(Entity<TemperatureDamageComponent> entity, DamageSpecifier damage)
    {
        return _woundDamageRouting.TryRouteDistributedDamage(entity, damage, TargetBodyPart.All,
            DamageDistribution.SplitByPartWeight, ignoreResistances: true, interruptsDoAfters: false);
    }
}
