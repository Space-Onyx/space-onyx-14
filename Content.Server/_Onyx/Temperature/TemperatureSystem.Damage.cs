using Content.Shared.Damage;
using Content.Shared.Temperature.Components;
using Content.Shared._Onyx.Targeting;
using Content.Shared._Onyx.Wounds;

namespace Content.Server.Temperature.Systems;

public sealed partial class TemperatureSystem
{
    [Dependency] private WoundDamageRoutingSystem _woundDamageRouting = default!;

    private partial bool TryApplyLocalizedTemperatureDamage(Entity<TemperatureDamageComponent> entity, DamageSpecifier damage)
    {
        return HasComp<WoundHostComponent>(entity)
            && _woundDamageRouting.TryApplyDistributedDamage(entity, damage, TargetBodyPart.All,
                DamageDistribution.SplitByPartWeight, ignoreResistances: true, interruptsDoAfters: false);
    }
}
