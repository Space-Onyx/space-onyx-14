// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Traits;

[RegisterComponent, NetworkedComponent]
public sealed partial class VoraciousComponent : Component
{
    [DataField]
    public float EatingDelayMultiplier = 0.5f;
}
