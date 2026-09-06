// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Server._Onyx.Salvage.Procedural.Components;
using Content.Server.Parallax;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Map.Enumerators;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Salvage.Procedural.Systems;

public sealed partial class LavalandBiomeWarmupSystem : EntitySystem
{
    [Dependency] private BiomeSystem _biome = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private const int BiomeChunkSize = 8;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesBefore.Add(typeof(BiomeSystem));
        SubscribeLocalEvent<LavalandBiomeWarmupComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<LavalandBiomeWarmupComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<BiomeComponent>(ent, out var biome))
            return;

        var terrainChunks = new ChunkIndicesEnumerator(ent.Comp.Area, BiomeChunkSize);
        while (terrainChunks.MoveNext(out var terrainChunk))
            ent.Comp.TerrainChunks.Enqueue(terrainChunk.Value * BiomeChunkSize);

        foreach (var layer in biome.MarkerLayers)
        {
            var prototype = _prototypes.Index(layer);
            var chunks = new ChunkIndicesEnumerator(ent.Comp.Area, prototype.Size);
            while (chunks.MoveNext(out var chunk))
                ent.Comp.MarkerChunks.Enqueue((layer, (chunk * prototype.Size).Value));
        }
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<LavalandBiomeWarmupComponent, BiomeComponent>();
        while (query.MoveNext(out var uid, out var warmup, out var biome))
        {
            if (warmup.Complete)
                continue;

            if (warmup.TerrainChunks.TryDequeue(out var terrainChunk))
                warmup.PinnedTerrainChunks.Add(terrainChunk);

            while (warmup.MarkerChunks.TryPeek(out var entry) &&
                   _biome.IsLavalandMarkerChunkComplete(uid, biome, entry.Layer, entry.Chunk))
                warmup.MarkerChunks.Dequeue();

            if (warmup.MarkerChunks.TryPeek(out var next))
                _biome.PreloadLavalandMarkerChunk(biome, next.Layer, next.Chunk);

            if (warmup.TerrainChunks.Count == 0 &&
                warmup.MarkerChunks.Count == 0 &&
                BiomeSystem.AreLavalandTerrainChunksLoaded(biome, warmup.PinnedTerrainChunks))
                warmup.Complete = true;
        }
    }
}
