// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Onyx.Telecommunications.Components;

/// <summary>
/// Aggregates receiver traffic before buses and server traffic before broadcasters.
/// </summary>
[RegisterComponent]
public sealed partial class TelecomHubComponent : Component
{
    /// <summary>
    /// Maximum number of receiver and server inputs.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxInputs = 8;

    /// <summary>
    /// Maximum number of bus and broadcaster outputs.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxOutputs = 8;

    /// <summary>
    /// Shapes signal loss as mechanical condition falls.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float RouteLossExponent = 2f;

    /// <summary>
    /// Scales signal loss caused by hub wear.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float RouteLossChanceMultiplier = 0.5f;
}
