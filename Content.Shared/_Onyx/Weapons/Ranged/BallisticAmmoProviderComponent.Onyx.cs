// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
namespace Content.Shared.Weapons.Ranged.Components;

public sealed partial class BallisticAmmoProviderComponent
{
    /// <summary>
    /// Whether spent ammo is automatically ejected after each shot. If false, manual cycling is required.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AutoCycle = true;
}
