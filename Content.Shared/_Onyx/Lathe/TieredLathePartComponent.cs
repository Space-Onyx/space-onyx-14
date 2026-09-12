// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

namespace Content.Shared._Onyx.Lathe;

[RegisterComponent]
public sealed partial class TieredLathePartComponent : Component
{
    /// <summary>
    /// Original production-time multiplier before tier effects.
    /// </summary>
    [DataField]
    public float BaseTimeMultiplier;

    /// <summary>
    /// Original material-use multiplier before tier effects.
    /// </summary>
    [DataField]
    public float BaseMaterialUseMultiplier;

    /// <summary>
    /// Original local material storage limit.
    /// </summary>
    [DataField]
    public int? BaseStorageLimit;
}
