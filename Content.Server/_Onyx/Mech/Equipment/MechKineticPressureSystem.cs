using Content.Server.Atmos.EntitySystems;
using Content.Shared._Onyx.Mech;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Upgrades;
using Content.Shared.Weapons.Ranged.Upgrades.Components;

namespace Content.Server.Mech.Port;

public sealed partial class MechKineticPressureSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private GunUpgradeSystem _upgrades = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MechKineticUpgradeComponent, AmmoShotEvent>(OnShot,
            after: [typeof(GunUpgradeSystem)]);
    }

    private void OnShot(Entity<MechKineticUpgradeComponent> gun, ref AmmoShotEvent args)
    {
        if (!TryComp<UpgradeableGunComponent>(gun, out var upgradeable))
            return;

        var pressure = _atmosphere.GetTileMixture((gun.Owner, Transform(gun)))?.Pressure ?? 0f;
        var modifier = pressure is >= 20.265f and <= 50.6625f ? 2f : 1f;
        foreach (var upgrade in _upgrades.GetCurrentUpgrades((gun, upgradeable)))
        {
            if (!TryComp<MechKineticPressureUpgradeComponent>(upgrade, out var pressureUpgrade) ||
                pressure < pressureUpgrade.LowerBound ||
                pressure > pressureUpgrade.UpperBound)
                continue;

            modifier = pressureUpgrade.Modifier;
            break;
        }

        if (modifier == 1f)
            return;

        foreach (var uid in args.FiredProjectiles)
        {
            if (!TryComp<ProjectileComponent>(uid, out var projectile))
                continue;

            projectile.Damage *= modifier;
        }
    }
}
