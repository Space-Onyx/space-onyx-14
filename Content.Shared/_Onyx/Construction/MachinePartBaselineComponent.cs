// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

namespace Content.Shared._Onyx.Construction;

[RegisterComponent]
public sealed partial class MachinePartBaselineComponent : Component
{
    /// <summary>
    /// Original machine values used to avoid cumulative upgrades.
    /// </summary>
    [DataField]
    public Dictionary<string, float> Values = new();
}
