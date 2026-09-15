// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Overlays;

/// <summary>
///     Replicated mood-to-visual bridge: the client overlay reads
///     <see cref="SaturationScale"/> and tints the viewport accordingly.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SaturationScaleOverlayComponent : Component
{
    /// <summary>
    ///     Reference level the current mood is normalized against.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float NeutralMoodThreshold = 1f;

    /// <summary>
    ///     Target saturation multiplier; 1 is untouched color.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SaturationScale = 1f;

    /// <summary>
    ///     How fast the visual catches up with the target each second.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FadeInMultiplier = 0.1f;
}
