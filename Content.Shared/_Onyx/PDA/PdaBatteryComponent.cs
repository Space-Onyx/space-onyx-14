// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

namespace Content.Shared._Onyx.PDA;

/// <summary>
/// Marks a PDA as battery-powered (SS13-style charge).
/// Drain is applied through RefreshChargeRateEvent in the server partial
/// of PdaSystem, no per-tick charge writes.
/// Actual energy lives in the PowerCellSlot cell on the same entity.
/// </summary>
[RegisterComponent]
public sealed partial class PdaBatteryComponent : Component
{
    /// <summary>
    /// Passive drain in watts (firmware, clock). Always applied while a cell is inside.
    /// Tuned SS13-style: a small cell lasts the whole shift on standby.
    /// </summary>
    [DataField]
    public float IdleDraw = 0.01f;

    /// <summary>
    /// Extra drain in watts while the PDA is powered on.
    /// </summary>
    [DataField]
    public float PoweredDraw = 0.1f;

    /// <summary>
    /// Extra drain in watts while the flashlight is on.
    /// </summary>
    [DataField]
    public float LightDraw = 0.4f;

    /// <summary>
    /// Charge percent below which the UI shows a low-battery warning.
    /// </summary>
    [DataField]
    public float LowThreshold = 0.25f;

    [ViewVariables]
    public bool PoweredOn;
}
