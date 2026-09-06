// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared.Parallax.Biomes.Markers;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Salvage.Procedural.Components;

[RegisterComponent]
public sealed partial class LavalandBiomeWarmupComponent : Component
{
    /// <summary>
    /// Arrival area generated before the Lavaland map becomes available.
    /// </summary>
    public Box2 Area;

    /// <summary>
    /// Terrain chunks waiting to be added to the warmup set.
    /// </summary>
    public readonly Queue<Vector2i> TerrainChunks = new();

    /// <summary>
    /// Terrain chunks kept loaded for the first arrival.
    /// </summary>
    public readonly HashSet<Vector2i> PinnedTerrainChunks = new();

    /// <summary>
    /// Marker chunks waiting for time-sliced generation.
    /// </summary>
    public readonly Queue<(ProtoId<BiomeMarkerLayerPrototype> Layer, Vector2i Chunk)> MarkerChunks = new();

    /// <summary>
    /// Whether terrain and marker warmup has finished.
    /// </summary>
    public bool Complete;
}
