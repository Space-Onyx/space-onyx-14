using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Ranged.Upgrades.Components;

/// <summary>
/// A <see cref="GunUpgradeComponent"/> for increasing the damage of a gun's projectile.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(GunUpgradeSystem))]
public sealed partial class GunUpgradeDamageComponent : Component
{
    // Onyx-start: PKA damage modkits use Goob's multiplicative tuning.
    [DataField]
    public float Modifier = 1;
    // Onyx-end

    /// <summary>
    /// Additional damage added onto the projectile's base damage.
    /// </summary>
    [DataField]
    public DamageSpecifier Damage = new();
}
