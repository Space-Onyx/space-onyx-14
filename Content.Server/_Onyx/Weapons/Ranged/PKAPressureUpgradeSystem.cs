using Content.Server._Onyx.Salvage.Pressure;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Onyx.Weapons.Ranged;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Upgrades;
using Content.Shared.Weapons.Ranged.Upgrades.Components;

namespace Content.Server._Onyx.Weapons.Ranged;

public sealed partial class PKAPressureUpgradeSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private GunUpgradeSystem _upgrades = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PressureDamageChangeComponent, AmmoShotEvent>(OnShot);
    }

    private void OnShot(Entity<PressureDamageChangeComponent> gun, ref AmmoShotEvent args)
    {
        if (!gun.Comp.ApplyToProjectiles)
            return;

        var lowerBound = gun.Comp.LowerBound;
        var upperBound = gun.Comp.UpperBound;
        var applyWhenInRange = gun.Comp.ApplyWhenInRange;
        var modifier = gun.Comp.AppliedModifier;

        if (TryComp<UpgradeableGunComponent>(gun, out var upgradeable))
        {
            foreach (var upgrade in _upgrades.GetCurrentUpgrades((gun, upgradeable)))
            {
                if (!TryComp<GunUpgradePressureComponent>(upgrade, out var pressureUpgrade))
                    continue;

                lowerBound = pressureUpgrade.NewLowerBound ?? lowerBound;
                upperBound = pressureUpgrade.NewUpperBound ?? upperBound;
                applyWhenInRange = pressureUpgrade.NewApplyWhenInRange ?? applyWhenInRange;
                modifier = pressureUpgrade.NewAppliedModifier ?? modifier;
                break;
            }
        }

        var pressure = _atmosphere.GetTileMixture((gun.Owner, Transform(gun)))?.Pressure ?? 0f;
        if ((pressure >= lowerBound && pressure <= upperBound) != applyWhenInRange)
            return;

        foreach (var projectileUid in args.FiredProjectiles)
            if (TryComp<ProjectileComponent>(projectileUid, out var projectile))
                projectile.Damage *= modifier;
    }
}
