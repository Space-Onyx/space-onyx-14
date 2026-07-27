using Content.Server.Atmos.EntitySystems;
using Content.Shared.Damage;
using Content.Shared._Onyx.Mech;
using Content.Shared.Projectiles;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Upgrades;
using Content.Shared.Weapons.Ranged.Upgrades.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Mech.Port;

public sealed partial class MechKineticPressureSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private GunUpgradeSystem _upgrades = default!;
    [Dependency] private TagSystem _tags = default!;
    private static readonly ProtoId<TagPrototype> GunUpgradeDamage = "GunUpgradeDamage";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MechKineticUpgradeComponent, GunShotEvent>(OnShot,
            after: [typeof(GunUpgradeSystem)]);
    }

    private void OnShot(Entity<MechKineticUpgradeComponent> gun, ref GunShotEvent args)
    {
        if (!TryComp<UpgradeableGunComponent>(gun, out var upgradeable))
            return;

        var pressure = _atmosphere.GetTileMixture((gun.Owner, Transform(gun)))?.Pressure ?? 0f;
        var modifier = pressure is >= 20.265f and <= 50.6625f ? 2f : 1f;
        var damageUpgrade = false;
        foreach (var upgrade in _upgrades.GetCurrentUpgrades((gun, upgradeable)))
        {
            if (_tags.HasTag(upgrade, GunUpgradeDamage))
                damageUpgrade = true;
            if (!TryComp<MechKineticPressureUpgradeComponent>(upgrade, out var pressureUpgrade) ||
                pressure < pressureUpgrade.LowerBound ||
                pressure > pressureUpgrade.UpperBound)
                continue;

            modifier = pressureUpgrade.Modifier;
            break;
        }

        if (modifier == 1f && !damageUpgrade)
            return;

        foreach (var (uid, _) in args.Ammo)
        {
            if (!TryComp<ProjectileComponent>(uid, out var projectile))
                continue;

            if (damageUpgrade)
            {
                projectile.Damage -= new DamageSpecifier
                {
                    DamageDict =
                    {
                        ["Blunt"] = 10,
                        ["Structural"] = 15,
                    }
                };
                projectile.Damage *= 1.25f;
            }

            projectile.Damage *= modifier;
        }
    }
}
