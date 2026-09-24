// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
namespace Content.Shared.Weapons.Melee;

public sealed partial class MeleeWeaponComponent
{
    /// <summary>
    /// Whether the weapon can perform heavy attacks.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CanHeavyAttack = true;

    /// <summary>
    /// Whether the weapon can perform wide swinging attacks.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CanWideSwing = true;

    /// <summary>
    /// Rotation of the light attack animation.
    /// 0 degrees means the top faces the attacker.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Angle AnimationRotation = Angle.Zero;
}
