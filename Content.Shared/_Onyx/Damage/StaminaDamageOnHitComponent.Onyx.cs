// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
namespace Content.Shared.Damage.Components;

public sealed partial class StaminaDamageOnHitComponent
{
    /// <summary>
    /// Stamina damage dealt over time after the hit.
    /// </summary>
    [DataField]
    public float Overtime;

    /// <summary>
    /// Multiplier applied to the instant damage on light attacks.
    /// </summary>
    [DataField]
    public float LightAttackDamageMultiplier = 1f;

    /// <summary>
    /// Multiplier applied to the overtime damage on light attacks.
    /// </summary>
    [DataField]
    public float LightAttackOvertimeDamageMultiplier = 1f;
}
