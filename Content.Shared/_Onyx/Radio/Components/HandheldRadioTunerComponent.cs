// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Radio.Components;

/// <summary>
/// Tunable frequency data for a handheld radio, SS13-style (BlueMoon Radio tGUI).
/// The frequency dial is free within range; the active channel is resolved
/// from the dialed frequency against <see cref="SupportedChannels"/>.
/// Unmatched frequencies are static: nothing is heard or transmitted.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HandheldRadioTunerComponent : Component
{
    /// <summary>
    /// Currently dialed frequency. Resolved to a channel by exact match.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Frequency = 133f;

    /// <summary>
    /// When true, the frequency cannot be changed (specialist radios, SS13 freqlock).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool FrequencyLocked;

    /// <summary>
    /// Lower bound of the tunable spectrum.
    /// </summary>
    [DataField]
    public float MinFrequency = 130f;

    /// <summary>
    /// Upper bound of the tunable spectrum.
    /// </summary>
    [DataField]
    public float MaxFrequency = 136f;

    /// <summary>
    /// Frequency nudge per -/+ press, SS13 NumberInput step.
    /// </summary>
    [DataField]
    public float Step = 0.2f;

    /// <summary>
    /// Channels this radio can resolve by frequency, SS13 encryption-key style.
    /// Anything else dialed is static.
    /// </summary>
    [DataField]
    public List<ProtoId<RadioChannelPrototype>> SupportedChannels = new() { "Handheld" };
}
