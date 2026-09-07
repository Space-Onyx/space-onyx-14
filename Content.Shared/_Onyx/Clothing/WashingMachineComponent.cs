// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Clothing;

[RegisterComponent]
public sealed partial class WashingMachineComponent : Component
{
    public const string ContainerId = "entity_storage";

    [DataField]
    public float WashTime = 20f;

    [DataField]
    public string CleanerReagent = "Water";

    [DataField]
    public FixedPoint2 WashAmount = FixedPoint2.New(100);

    [DataField]
    public SoundSpecifier FinishSound = new SoundPathSpecifier("/Audio/Machines/ding.ogg");

    public float RemainingTime;

    public bool IsWashing;
}

[Serializable, NetSerializable]
public enum WashingMachineVisuals : byte
{
    Washing,
}

[Serializable, NetSerializable]
public enum WashingMachineVisualLayers : byte
{
    Washing,
}
