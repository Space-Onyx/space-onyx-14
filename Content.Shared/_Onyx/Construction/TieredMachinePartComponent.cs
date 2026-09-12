// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

namespace Content.Shared._Onyx.Construction;

[RegisterComponent]
public sealed partial class TieredMachinePartComponent : Component
{
    /// <summary>
    /// Machine function improved by this part.
    /// </summary>
    [DataField(required: true)]
    public MachinePartKind Kind;

    /// <summary>
    /// Efficiency level used by compatible machines.
    /// </summary>
    [DataField]
    public int Tier = 1;
}

public enum MachinePartKind : byte
{
    Servo,
    Capacitor,
    MatterBin,
    Scanner,
    Laser,
}
