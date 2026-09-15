// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.Mood;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Traits.Mood.Components;

/// <summary>
///     Permanent moodlets granted by a character trait on spawn.
/// </summary>
[RegisterComponent]
public sealed partial class MoodTraitComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<MoodEffectPrototype>> MoodEffects = new();
}
