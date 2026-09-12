// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.Construction;

#pragma warning disable IDE0130
namespace Content.Shared.Construction.Components;

public sealed partial class MachineBoardComponent
{
    /// <summary>
    /// Typed machine parts required to construct this machine.
    /// </summary>
    [DataField]
    public Dictionary<MachinePartKind, int> PartRequirements = new();
}
