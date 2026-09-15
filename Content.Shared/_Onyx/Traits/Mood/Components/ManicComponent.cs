// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Traits.Mood.Components;

/// <summary>
///     Scales every mood shift by a random factor within bounds,
///     softening lows and amplifying highs unpredictably.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ManicComponent : Component
{
    /// <summary>
    ///     Lower end of the random multiplier range.
    /// </summary>
    [DataField]
    public float LowerMultiplier = 0.7f;

    /// <summary>
    ///     Upper end of the random multiplier range.
    /// </summary>
    [DataField]
    public float UpperMultiplier = 1.3f;
}
