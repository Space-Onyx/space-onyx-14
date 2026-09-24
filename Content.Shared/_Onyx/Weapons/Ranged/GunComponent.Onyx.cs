// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
namespace Content.Shared.Weapons.Ranged.Components;

public sealed partial class GunComponent
{
    /// <summary>
    /// Whether the gun keeps its burst target instead of retargeting while a burst is active.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool LockOnTargetBurst;
}
