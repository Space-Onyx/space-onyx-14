// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Traits.Mood.Components;

/// <summary>
///     Adds a random flat offset to every mood update.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MercurialComponent : Component
{
    /// <summary>
    ///     Lower end of the random offset range.
    /// </summary>
    [DataField]
    public float LowerMood = -10f;

    /// <summary>
    ///     Upper end of the random offset range.
    /// </summary>
    [DataField]
    public float UpperMood = 10f;
}
