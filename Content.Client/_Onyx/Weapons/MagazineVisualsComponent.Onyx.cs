// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
namespace Content.Client.Weapons.Ranged.Components;

public sealed partial class MagazineVisualsComponent
{
    /// <summary>
    /// Whether the zero step is only shown when no ammo is left.
    /// </summary>
    [DataField]
    public bool ZeroNoAmmo;
}
