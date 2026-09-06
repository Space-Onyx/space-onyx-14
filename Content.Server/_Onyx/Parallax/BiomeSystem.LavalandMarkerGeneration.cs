// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Threading.Tasks;
using Content.Server._Onyx.Salvage.Procedural.Components;
using Content.Shared.GameTicking;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax.Biomes.Markers;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

#pragma warning disable IDE0130
namespace Content.Server.Parallax;

public sealed partial class BiomeSystem
{
    private const double LavalandMarkerGenerationTime = 0.001;
    private JobQueue _lavalandMarkerQueue = new(LavalandMarkerGenerationTime);
    private readonly Dictionary<(EntityUid Grid, string Layer, Vector2i Chunk), LavalandMarkerJob> _lavalandMarkerJobs = new();
    private readonly HashSet<(EntityUid Grid, string Layer, Vector2i Chunk)> _failedLavalandMarkerJobs = new();
    private readonly List<(EntityUid Grid, string Layer, Vector2i Chunk)> _finishedLavalandMarkerJobs = new();

    private void ProcessLavalandMarkerGeneration()
    {
        _lavalandMarkerQueue.Process();
        _finishedLavalandMarkerJobs.Clear();
        foreach (var (key, job) in _lavalandMarkerJobs)
        {
            if (job.Status != JobStatus.Finished)
                continue;

            _finishedLavalandMarkerJobs.Add(key);
            if (job.Exception != null || TerminatingOrDeleted(key.Grid))
            {
                _failedLavalandMarkerJobs.Add(key);
                Log.Error($"Failed to warm Lavaland marker chunk {key.Chunk} for layer '{key.Layer}': {job.Exception}");
            }
            else
                CommitLavalandMarkerGeneration(job);
        }

        foreach (var key in _finishedLavalandMarkerJobs)
            _lavalandMarkerJobs.Remove(key);
    }

    private void OnLavalandMarkerGenerationCleanup(RoundRestartCleanupEvent _)
    {
        _lavalandMarkerQueue = new JobQueue(LavalandMarkerGenerationTime);
        _lavalandMarkerJobs.Clear();
        _failedLavalandMarkerJobs.Clear();
        _finishedLavalandMarkerJobs.Clear();
    }

    private bool TryQueueLavalandMarkerChunks(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        string layer,
        HashSet<Vector2i> chunks,
        int seed,
        int layerIndex)
    {
        if (!HasComp<LavalandPlanetComponent>(gridUid))
            return false;

        foreach (var chunk in chunks)
            QueueLavalandMarkerChunk(component, gridUid, grid, layer, chunk, seed, layerIndex);
        return true;
    }

    private void QueueLavalandMarkerChunk(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        string layer,
        Vector2i chunk,
        int seed,
        int layerIndex)
    {
        var key = (gridUid, layer, chunk);
        if (_lavalandMarkerJobs.ContainsKey(key) ||
            _failedLavalandMarkerJobs.Contains(key) ||
            component.LoadedMarkers.TryGetValue(layer, out var loaded) && loaded.Contains(chunk))
            return;

        var prototype = ProtoMan.Index<BiomeMarkerLayerPrototype>(layer);
        var buffer = (int) (prototype.Radius / 2f);
        var bounds = new Box2i(chunk + buffer, chunk + prototype.Size - buffer);
        var count = Math.Min((int) (bounds.Area / (prototype.Radius * prototype.Radius)), prototype.MaxCount);
        if (count <= 0)
        {
            _failedLavalandMarkerJobs.Add(key);
            return;
        }

        var job = new LavalandMarkerJob(this, component, gridUid, grid, layer, prototype, chunk, bounds, count,
            seed + chunk.X * ChunkSize + chunk.Y + layerIndex,
            component.ForcedMarkerLayers.Contains(layer));
        _lavalandMarkerJobs.Add(key, job);
        _lavalandMarkerQueue.EnqueueJob(job);
    }

    public void PreloadLavalandMarkerChunk(
        BiomeComponent component,
        ProtoId<BiomeMarkerLayerPrototype> layer,
        Vector2i chunk)
    {
        _markerChunks.GetOrNew(component).GetOrNew(layer).Add(chunk);
    }

    private void AddLavalandWarmupChunks(EntityUid gridUid, BiomeComponent component)
    {
        if (TryComp<LavalandBiomeWarmupComponent>(gridUid, out var warmup))
            _activeChunks[component].UnionWith(warmup.PinnedTerrainChunks);
    }

    public static bool AreLavalandTerrainChunksLoaded(BiomeComponent component, HashSet<Vector2i> chunks)
    {
        return component.LoadedChunks.IsSupersetOf(chunks);
    }

    public bool HasLavalandMarkerGenerationFailed(EntityUid gridUid)
    {
        foreach (var key in _failedLavalandMarkerJobs)
        {
            if (key.Grid == gridUid)
                return true;
        }

        return false;
    }

    public bool IsLavalandMarkerChunkComplete(
        EntityUid gridUid,
        BiomeComponent component,
        ProtoId<BiomeMarkerLayerPrototype> layer,
        Vector2i chunk)
    {
        return component.LoadedMarkers.TryGetValue(layer, out var loaded) && loaded.Contains(chunk);
    }

    private bool GetLavalandMarkerCandidate(
        EntityUid gridUid,
        BiomeComponent biome,
        MapGridComponent grid,
        BiomeMarkerLayerPrototype layer,
        bool forced,
        Vector2i node,
        out EntityUid? existing,
        out string? mask)
    {
        var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, node);
        anchored.MoveNext(out existing);
        if (!forced && existing != null)
        {
            mask = null;
            return false;
        }

        TryGetEntity(node, biome, (gridUid, grid), out var prototype);
        if (layer.EntityMask.Count > 0 && (prototype == null || !layer.EntityMask.ContainsKey(prototype)) ||
            prototype != null && layer.Prototype != null)
        {
            mask = null;
            return false;
        }

        mask = prototype;
        return true;
    }

    private void CommitLavalandMarkerGeneration(LavalandMarkerJob job)
    {
        foreach (var entity in job.Existing)
            Del(entity);

        job.Component.LoadedMarkers.GetOrNew(job.Layer).Add(job.Chunk);
        foreach (var node in job.SpawnSet.Keys)
        {
            var origin = SharedMapSystem.GetChunkIndices(node, ChunkSize) * ChunkSize;
            job.Component.PendingMarkers.GetOrNew(origin).GetOrNew(job.Layer).Add(node);
        }
    }

    private sealed class LavalandMarkerJob : Job<object?>
    {
        private readonly BiomeSystem _system;
        private readonly BiomeComponent _biome;
        private readonly EntityUid _gridUid;
        private readonly MapGridComponent _grid;
        private readonly BiomeMarkerLayerPrototype _prototype;
        private readonly Box2i _bounds;
        private readonly int _count;
        private readonly RobustRandom _random = new();
        private readonly HashSet<Vector2i> _remaining = new();
        private readonly Dictionary<Vector2i, EntityUid?> _entities = new();
        private readonly Dictionary<Vector2i, string?> _masks = new();
        private readonly List<Vector2i> _frontier = new();
        private int _x;
        private int _y;
        private int _group;
        private int _groupSize;
        private bool _scanning = true;

        public readonly BiomeComponent Component;
        public readonly string Layer;
        public readonly Vector2i Chunk;
        public readonly bool Forced;
        public readonly Dictionary<Vector2i, string?> SpawnSet = new();
        public readonly HashSet<EntityUid> Existing = new();

        public LavalandMarkerJob(
            BiomeSystem system,
            BiomeComponent biome,
            EntityUid gridUid,
            MapGridComponent grid,
            string layer,
            BiomeMarkerLayerPrototype prototype,
            Vector2i chunk,
            Box2i bounds,
            int count,
            int seed,
            bool forced) : base(LavalandMarkerGenerationTime)
        {
            _system = system;
            _biome = Component = biome;
            _gridUid = gridUid;
            _grid = grid;
            Layer = layer;
            _prototype = prototype;
            Chunk = chunk;
            _bounds = bounds;
            _count = count;
            Forced = forced;
            _random.SetSeed(seed);
            _x = bounds.Left;
            _y = bounds.Bottom;
        }

        protected override async Task<object?> Process()
        {
            while (_scanning)
            {
                var node = new Vector2i(_x, _y);
                if (_system.GetLavalandMarkerCandidate(_gridUid, _biome, _grid, _prototype, Forced, node,
                        out var existing, out var mask))
                {
                    _remaining.Add(node);
                    _entities[node] = existing;
                    _masks[node] = mask;
                }

                if (++_y >= _bounds.Top)
                {
                    _y = _bounds.Bottom;
                    if (++_x >= _bounds.Right)
                        _scanning = false;
                }

                await SuspendIfOutOfTime();
            }

            while (_group < _count && _remaining.Count > 0)
            {
                if (_groupSize == 0)
                {
                    _frontier.Clear();
                    _groupSize = _random.Next(_prototype.MinGroupSize, _prototype.MaxGroupSize + 1);
                }

                if (_frontier.Count == 0)
                {
                    var start = _random.Pick(_remaining);
                    _remaining.Remove(start);
                    _frontier.Add(start);
                }

                var index = _random.Next(_frontier.Count);
                var current = _frontier[index];
                _frontier.RemoveAt(index);
                _remaining.Remove(current);
                SpawnSet[current] = _masks[current];
                if (_entities[current] is { } existing)
                    Existing.Add(existing);
                if (--_groupSize == 0)
                    _group++;

                for (var x = -1; x <= 1; x++)
                for (var y = -1; y <= 1; y++)
                {
                    var neighbor = new Vector2i(current.X + x, current.Y + y);
                    if (!_frontier.Contains(neighbor) && _remaining.Contains(neighbor))
                        _frontier.Add(neighbor);
                }

                await SuspendIfOutOfTime();
            }

            return null;
        }
    }
}
