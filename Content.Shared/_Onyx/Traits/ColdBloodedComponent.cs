// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

namespace Content.Shared._Onyx.Traits;

[RegisterComponent]
public sealed partial class ColdBloodedComponent : Component
{
    [DataField]
    public float MetabolismHeatMultiplier = 0.2f;
}

[ByRefEvent]
public record struct ModifyThermalRegulationEvent(
    float MetabolismHeatMultiplier = 1f,
    float ImplicitHeatingMultiplier = 1f,
    float ShiveringMultiplier = 1f);
