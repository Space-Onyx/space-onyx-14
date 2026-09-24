// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
namespace Content.Shared.EntityEffects.Effects.StatusEffects;

public sealed partial class ModifyStatusEffect
{
    /// <summary>
    /// Whether adding the effect refreshes its duration when it is already present.
    /// </summary>
    [DataField]
    public bool Refresh;
}

public sealed partial class GenericStatusEffect
{
    /// <summary>
    /// Whether adding the effect refreshes its duration when it is already present.
    /// </summary>
    [DataField]
    public bool Refresh;
}
