// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

#pragma warning disable IDE0130
namespace Content.Shared._Onyx.BluespaceMining;

public sealed partial class BluespaceMiningMachineComponent
{
    [DataField]
    public float ProductionSpeedMultiplier = 1f;

    [DataField]
    public float ProductionAmountMultiplier = 1f;

    [DataField]
    public float CoreDamageMultiplier = 1f;

    [DataField]
    public float InstabilityMultiplier = 1f;

    [DataField]
    public float TemperatureEffectMultiplier = 1f;
}
