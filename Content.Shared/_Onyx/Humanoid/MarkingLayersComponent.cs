// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Humanoid;

/// <summary>
/// Client-side record of sprite layers added by humanoid markings.
/// Marking art can be bigger than the standard 32x32 frame, which inflates
/// the engine sprite bounds and breaks everything measured from them.
/// Gameplay code should use <c>MarkingBoundsHelper</c> instead of raw bounds.
/// Never networked, only maintained on the client by <c>VisualBodySystem</c>.
/// </summary>
[RegisterComponent]
public sealed partial class MarkingLayersComponent : Component
{
    [DataField]
    public HashSet<string> LayerIds = new();
}
