// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Traits.Mood.Components;

/// <summary>
///     Nullifies every mood shift; the bearer always stays neutral.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DeadEmotionsComponent : Component;
