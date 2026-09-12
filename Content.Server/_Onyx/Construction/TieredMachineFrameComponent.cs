// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.Construction;

#pragma warning disable IDE0130
namespace Content.Server.Construction.Components;

public sealed partial class MachineFrameComponent
{
    /// <summary>
    /// Part categories required by current machine board.
    /// </summary>
    [ViewVariables]
    public readonly Dictionary<MachinePartKind, int> TieredPartRequirements = new();

    /// <summary>
    /// Required part categories already inserted into frame.
    /// </summary>
    [ViewVariables]
    public readonly Dictionary<MachinePartKind, int> TieredPartProgress = new();
}
