using System.Numerics;
using Content.Server._Onyx.Parallax.Components;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Ghost;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax.Biomes.Markers;
using Content.Shared.Parallax;
using Robust.Shared.Map.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Parallax;

public sealed partial class BiomeSystem
{
    private const string PlanetParallax = "bedrock";

    private readonly Dictionary<BiomeComponent, List<Vector2i>> _viewerChunks = new();
    private readonly List<(Vector2i Chunk, long Distance)> _orderedChunks = new();
    private readonly List<Vector2i> _chunksToUnload = new();

    private BiomeRuntimeOptimizationComponent? GetRuntimeOptimization(EntityUid gridUid)
    {
        return TryComp<BiomeRuntimeOptimizationComponent>(gridUid, out var optimization) ? optimization : null;
    }

    private int? GetMarkerChunkBudget(EntityUid gridUid)
    {
        return GetRuntimeOptimization(gridUid)?.MarkerChunksPerTick;
    }

    private void EnsurePlanetParallax(EntityUid mapUid)
    {
        EnsureComp<BiomeRuntimeOptimizationComponent>(mapUid);
        var parallax = EnsureComp<ParallaxComponent>(mapUid);
        parallax.Parallax = PlanetParallax;
        Dirty(mapUid, parallax);
    }

    private void InitializeRuntimeOptimization(BiomeComponent biome)
    {
        _viewerChunks.Add(biome, new List<Vector2i>());
    }

    private void ClearRuntimeOptimization()
    {
        _viewerChunks.Clear();
    }

    private void TrackViewerChunk(BiomeComponent biome, Vector2 worldPosition)
    {
        var tile = new Vector2i((int) MathF.Floor(worldPosition.X), (int) MathF.Floor(worldPosition.Y));
        _viewerChunks[biome].Add(SharedMapSystem.GetChunkIndices(tile, ChunkSize) * ChunkSize);
    }

    private List<(Vector2i Chunk, long Distance)> GetChunksToLoad(BiomeComponent component, HashSet<Vector2i> active)
    {
        _orderedChunks.Clear();
        foreach (var chunk in active)
            _orderedChunks.Add((chunk, GetViewerDistanceSquared(component, chunk)));

        _orderedChunks.Sort(static (a, b) => a.Distance.CompareTo(b.Distance));
        return _orderedChunks;
    }

    private bool TryLoadRuntimeOptimizedChunks(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        int seed,
        HashSet<Vector2i> active)
    {
        var optimization = GetRuntimeOptimization(gridUid);
        if (optimization == null)
            return false;

        var remainingLoads = Math.Max(0, optimization.ChunkLoadsPerTick);
        var remainingMarkerLoads = Math.Max(0, optimization.MarkerLoadsPerTick);
        var remainingMarkerNodes = Math.Max(0, optimization.MarkerNodesPerTick);
        foreach (var (chunk, _) in GetChunksToLoad(component, active))
        {
            if (remainingMarkerLoads > 0 && remainingMarkerNodes > 0 && component.PendingMarkers.ContainsKey(chunk))
            {
                remainingMarkerNodes -= LoadChunkMarkersRuntimeOptimized(
                    component,
                    gridUid,
                    grid,
                    chunk,
                    seed,
                    remainingMarkerNodes);
                remainingMarkerLoads--;
            }

            if (component.LoadedChunks.Contains(chunk) || remainingLoads <= 0)
                continue;

            component.LoadedChunks.Add(chunk);
            remainingLoads--;
            LoadChunk(component, gridUid, grid, chunk, seed);
        }

        return true;
    }

    private int LoadChunkMarkersRuntimeOptimized(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i chunk,
        int seed,
        int budget)
    {
        if (!component.PendingMarkers.TryGetValue(chunk, out var layers))
            return 0;

        component.ModifiedTiles.TryGetValue(chunk, out var modified);
        modified ??= _tilePool.Get();
        var loaded = 0;

        foreach (var (layer, nodes) in new List<KeyValuePair<string, List<Vector2i>>>(layers))
        {
            var layerProto = ProtoMan.Index<BiomeMarkerLayerPrototype>(layer);
            while (nodes.Count > 0 && loaded < budget)
            {
                var node = nodes[^1];
                nodes.RemoveAt(nodes.Count - 1);
                if (modified.Contains(node))
                    continue;

                if (TryGetBiomeTile(node, component.Layers, seed, (gridUid, grid), out var tile))
                    _mapSystem.SetTile(gridUid, grid, node, tile.Value);

                string? prototype;
                if (TryGetEntity(node, component, (gridUid, grid), out var biomePrototype) &&
                    layerProto.EntityMask.TryGetValue(biomePrototype, out var maskedPrototype))
                {
                    prototype = maskedPrototype;
                    RemoveLoadedBiomeEntity(component, chunk, node);
                }
                else
                {
                    prototype = layerProto.Prototype;
                }

                var uid = EntityManager.CreateEntityUninitialized(
                    prototype,
                    _mapSystem.GridTileToLocal(gridUid, grid, node));
                RemComp<GhostTakeoverAvailableComponent>(uid);
                RemComp<GhostRoleComponent>(uid);
                EntityManager.InitializeAndStartEntity(uid);
                modified.Add(node);
                loaded++;
            }

            if (nodes.Count == 0)
                layers.Remove(layer);
            if (loaded >= budget)
                break;
        }

        if (layers.Count == 0)
            component.PendingMarkers.Remove(chunk);

        if (modified.Count == 0)
        {
            component.ModifiedTiles.Remove(chunk);
            _tilePool.Return(modified);
        }
        else
        {
            component.ModifiedTiles[chunk] = modified;
        }

        return loaded;
    }

    private void RemoveLoadedBiomeEntity(BiomeComponent component, Vector2i chunk, Vector2i node)
    {
        if (!component.LoadedEntities.TryGetValue(chunk, out var entities))
            return;

        foreach (var (uid, origin) in new List<KeyValuePair<EntityUid, Vector2i>>(entities))
        {
            if (origin != node)
                continue;

            entities.Remove(uid);
            Del(uid);
            return;
        }
    }

    private long GetViewerDistanceSquared(BiomeComponent component, Vector2i chunk)
    {
        var nearest = long.MaxValue;
        foreach (var viewer in _viewerChunks[component])
        {
            var x = (long) chunk.X - viewer.X;
            var y = (long) chunk.Y - viewer.Y;
            nearest = Math.Min(nearest, x * x + y * y);
        }

        return nearest;
    }

    private List<Vector2i> GetMarkerChunksToBuild(
        BiomeComponent component,
        HashSet<Vector2i> chunks,
        Dictionary<string, HashSet<Vector2i>> loadedMarkers,
        string layer,
        int budget)
    {
        _orderedChunks.Clear();
        foreach (var chunk in chunks)
        {
            if (!loadedMarkers.TryGetValue(layer, out var loaded) || !loaded.Contains(chunk))
                _orderedChunks.Add((chunk, GetViewerDistanceSquared(component, chunk)));
        }

        _orderedChunks.Sort(static (a, b) => a.Distance.CompareTo(b.Distance));
        var count = Math.Min(_orderedChunks.Count, Math.Max(0, budget));
        var selected = new List<Vector2i>(count);
        for (var i = 0; i < count; i++)
            selected.Add(_orderedChunks[i].Chunk);

        return selected;
    }

    private IEnumerable<Vector2i> GetMarkerChunksToBuild(
        BiomeComponent component,
        HashSet<Vector2i> chunks,
        Dictionary<string, HashSet<Vector2i>> loadedMarkers,
        string layer,
        bool forced,
        int? budget,
        ref int remainingBudget)
    {
        if (forced || budget == null)
            return chunks;

        var selected = GetMarkerChunksToBuild(component, chunks, loadedMarkers, layer, remainingBudget);
        remainingBudget -= selected.Count;
        return selected;
    }

    private static bool CanUnloadChunk(
        BiomeRuntimeOptimizationComponent? optimization,
        Vector2i chunk,
        float frameTime,
        int remainingUnloads)
    {
        if (optimization == null)
            return true;

        var inactiveTime = optimization.InactiveChunks.GetValueOrDefault(chunk) + frameTime;
        optimization.InactiveChunks[chunk] = inactiveTime;
        return inactiveTime >= optimization.UnloadDelay && remainingUnloads > 0;
    }

    private bool TryUnloadRuntimeOptimizedChunks(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        int seed,
        float frameTime)
    {
        var optimization = GetRuntimeOptimization(gridUid);
        if (optimization == null)
            return false;

        var active = _activeChunks[component];
        List<(Vector2i, Tile)>? tiles = null;
        var remainingUnloads = Math.Max(0, optimization.ChunkUnloadsPerTick);
        _chunksToUnload.Clear();
        foreach (var chunk in component.LoadedChunks)
        {
            if (active.Contains(chunk))
            {
                optimization.InactiveChunks.Remove(chunk);
                continue;
            }

            if (!CanUnloadChunk(optimization, chunk, frameTime, remainingUnloads))
                continue;

            _chunksToUnload.Add(chunk);
            remainingUnloads--;
        }

        foreach (var chunk in _chunksToUnload)
        {
            tiles ??= new List<(Vector2i, Tile)>(ChunkSize * ChunkSize);
            UnloadChunk(component, gridUid, grid, chunk, seed, tiles);
            optimization.InactiveChunks.Remove(chunk);
        }

        return true;
    }

    public bool IsMarkerChunkLoaded(
        BiomeComponent component,
        ProtoId<BiomeMarkerLayerPrototype> layer,
        Vector2i chunk)
    {
        return component.LoadedMarkers.TryGetValue(layer, out var loaded) && loaded.Contains(chunk);
    }

    public bool PreloadMarkerChunk(
        EntityUid gridUid,
        BiomeComponent component,
        ProtoId<BiomeMarkerLayerPrototype> layer,
        Vector2i chunk)
    {
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var markerChunks = _markerChunks.GetOrNew(component);
        foreach (var markerLayer in component.MarkerLayers)
            markerChunks.GetOrNew(markerLayer);
        markerChunks[layer].Add(chunk);

        var layerIndex = 0;
        foreach (var markerLayer in component.MarkerLayers)
        {
            layerIndex++;
            if (markerLayer == layer)
                break;
        }

        QueueMarkerChunkGeneration(component, gridUid, grid, layer, chunk, component.Seed, layerIndex,
            component.ForcedMarkerLayers.Contains(layer));
        return false;
    }
}
