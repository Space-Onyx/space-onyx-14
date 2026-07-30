using System.Numerics;
using Content.Server._Onyx.Salvage.Procedural.Components;
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
    private readonly List<Vector2i> _orderedChunks = new();
    private bool _preloadingMarkerChunk;

    private LavalandBiomeOptimizationComponent? GetRuntimeOptimization(EntityUid gridUid)
    {
        return TryComp<LavalandBiomeOptimizationComponent>(gridUid, out var optimization) ? optimization : null;
    }

    private int? GetMarkerChunkBudget(EntityUid gridUid)
    {
        return _preloadingMarkerChunk ? null : GetRuntimeOptimization(gridUid)?.MarkerChunksPerTick;
    }

    private void EnsurePlanetParallax(EntityUid mapUid)
    {
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

    private IEnumerable<Vector2i> GetChunksToLoad(
        BiomeComponent component,
        HashSet<Vector2i> active,
        LavalandBiomeOptimizationComponent? optimization)
    {
        if (optimization == null)
            return active;

        _orderedChunks.Clear();
        _orderedChunks.AddRange(active);
        _orderedChunks.Sort((a, b) => GetViewerDistanceSquared(component, a).CompareTo(GetViewerDistanceSquared(component, b)));
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

        var remainingLoads = optimization.ChunkLoadsPerTick;
        var remainingMarkerLoads = optimization.MarkerLoadsPerTick;
        foreach (var chunk in GetChunksToLoad(component, active, optimization))
        {
            var immediate = IsImmediateChunk(component, chunk, optimization.ImmediateLoadRadius);
            if ((immediate || remainingMarkerLoads > 0) && component.PendingMarkers.ContainsKey(chunk))
            {
                LoadChunkMarkers(component, gridUid, grid, chunk, seed);
                if (!immediate)
                    remainingMarkerLoads--;
            }

            if (component.LoadedChunks.Contains(chunk) || (!immediate && remainingLoads <= 0))
                continue;

            component.LoadedChunks.Add(chunk);
            if (!immediate)
                remainingLoads--;
            LoadChunk(component, gridUid, grid, chunk, seed);
        }

        return true;
    }

    private bool IsImmediateChunk(BiomeComponent component, Vector2i chunk, int radius)
    {
        var maxDistance = radius * ChunkSize;
        foreach (var viewer in _viewerChunks[component])
        {
            if (Math.Abs(chunk.X - viewer.X) <= maxDistance && Math.Abs(chunk.Y - viewer.Y) <= maxDistance)
                return true;
        }

        return false;
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

    private static List<Vector2i> GetMarkerChunksToBuild(
        HashSet<Vector2i> chunks,
        Dictionary<string, HashSet<Vector2i>> loadedMarkers,
        string layer,
        int budget)
    {
        var selected = new List<Vector2i>(Math.Min(chunks.Count, budget));
        foreach (var chunk in chunks)
        {
            if (selected.Count >= budget)
                break;

            if (!loadedMarkers.TryGetValue(layer, out var loaded) || !loaded.Contains(chunk))
                selected.Add(chunk);
        }

        return selected;
    }

    private static IEnumerable<Vector2i> GetMarkerChunksToBuild(
        HashSet<Vector2i> chunks,
        Dictionary<string, HashSet<Vector2i>> loadedMarkers,
        string layer,
        bool forced,
        int? budget,
        ref int remainingBudget)
    {
        if (forced || budget == null)
            return chunks;

        var selected = GetMarkerChunksToBuild(chunks, loadedMarkers, layer, remainingBudget);
        remainingBudget -= selected.Count;
        return selected;
    }

    private static bool CanUnloadChunk(
        LavalandBiomeOptimizationComponent? optimization,
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
        var remainingUnloads = optimization.ChunkUnloadsPerTick;
        foreach (var chunk in component.LoadedChunks)
        {
            if (active.Contains(chunk))
            {
                optimization.InactiveChunks.Remove(chunk);
                continue;
            }

            if (!CanUnloadChunk(optimization, chunk, frameTime, remainingUnloads) || !component.LoadedChunks.Remove(chunk))
                continue;

            tiles ??= new List<(Vector2i, Tile)>(ChunkSize * ChunkSize);
            UnloadChunk(component, gridUid, grid, chunk, seed, tiles);
            optimization.InactiveChunks.Remove(chunk);
            remainingUnloads--;
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
        _preloadingMarkerChunk = true;
        try
        {
            BuildMarkerChunks(component, gridUid, grid, component.Seed);
        }
        finally
        {
            _preloadingMarkerChunk = false;
        }
        return true;
    }
}
