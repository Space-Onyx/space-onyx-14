// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Doors.Components;

/// <summary>
/// Marks a multi-tile-wide airlock. Atmosphere seals one tile per anchored entity,
/// so the <see cref="Systems.MultiTileAirlockAirtightSystem"/> keeps technical
/// blockers on the extra tiles and mirrors the door's air-block state onto them.
/// Offsets are in local space, before rotation.
/// </summary>
[RegisterComponent]
public sealed partial class MultiTileAirlockAirtightComponent : Component
{
    /// <summary>
    /// Technical blocker spawned on every extra tile.
    /// </summary>
    [DataField]
    public EntProtoId BlockerPrototype = "AirtightBlocker";

    /// <summary>
    /// Local-space offsets (pre-rotation) of the additional tiles to seal.
    /// </summary>
    [DataField]
    public List<Vector2i> TileOffsets = new() { new Vector2i(1, 0) };

    /// <summary>
    /// Live blockers owned by this door. Runtime only.
    /// </summary>
    [ViewVariables]
    public List<EntityUid> BlockerEntities = new();
}
