// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Mood;

/// <summary>
///     Server-authoritative mood snapshot replicated to clients.
///     Gameplay decisions live on the server; this only carries
///     the numbers client-side visuals (e.g. saturation) need.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NetMoodComponent : Component
{
    /// <summary>
    ///     Sum of every active moodlet, hidden ones included.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CurrentMood;

    /// <summary>
    ///     Sum of visible moodlets only; drives UI-facing readouts.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CurrentShownMood;

    /// <summary>
    ///     Mood level shifted by the neutral threshold; compared against thresholds.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CurrentMoodLevel;

    /// <summary>
    ///     Cached neutral threshold so clients can normalize visuals.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float NeutralMoodThreshold;

    /// <summary>
    ///     Current sanity value mirrored for debug/visual use.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CurrentSanity;
}
