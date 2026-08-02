using System.Threading.Tasks;
using Content.Shared.GameTicking;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax.Biomes.Markers;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server.Parallax;

public sealed partial class BiomeSystem
{
    private const double MarkerGenerationTime = 0.00025;
    private readonly JobQueue _markerGenerationQueue = new(MarkerGenerationTime);
    private readonly Dictionary<(EntityUid Grid, string Layer, Vector2i Chunk), MarkerGenerationJob> _markerJobs = new();

    private void ProcessMarkerGeneration()
    {
        _markerGenerationQueue.Process();
        foreach (var entry in new List<KeyValuePair<(EntityUid Grid, string Layer, Vector2i Chunk), MarkerGenerationJob>>(_markerJobs))
        {
            var key = entry.Key;
            var job = entry.Value;
            if (job.Status != JobStatus.Finished)
                continue;

            _markerJobs.Remove(key);
            if (job.Exception == null && !TerminatingOrDeleted(key.Grid))
                CommitMarkerGeneration(job);
        }
    }

    private void OnMarkerGenerationCleanup(RoundRestartCleanupEvent _)
    {
        _markerJobs.Clear();
    }

    private void QueueMarkerChunkGeneration(
        BiomeComponent component,
        EntityUid gridUid,
        MapGridComponent grid,
        string layer,
        Vector2i chunk,
        int seed,
        int layerIndex,
        bool forced)
    {
        var key = (gridUid, layer, chunk);
        if (_markerJobs.ContainsKey(key) ||
            component.LoadedMarkers.TryGetValue(layer, out var loaded) && loaded.Contains(chunk))
            return;

        var prototype = ProtoMan.Index<BiomeMarkerLayerPrototype>(layer);
        var buffer = (int) (prototype.Radius / 2f);
        var bounds = new Box2i(chunk + buffer, chunk + prototype.Size - buffer);
        var count = Math.Min((int) (bounds.Area / (prototype.Radius * prototype.Radius)), prototype.MaxCount);
        if (count <= 0)
            return;

        var job = new MarkerGenerationJob(this, component, gridUid, grid, layer, prototype, chunk, bounds,
            count, seed + chunk.X * ChunkSize + chunk.Y + layerIndex, forced);
        _markerJobs.Add(key, job);
        _markerGenerationQueue.EnqueueJob(job);
    }

    private bool GetMarkerCandidate(
        EntityUid gridUid,
        BiomeComponent biome,
        MapGridComponent grid,
        BiomeMarkerLayerPrototype layer,
        bool forced,
        Vector2i node,
        out EntityUid? existing,
        out string? mask)
    {
        var enumerator = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid, grid, node);
        enumerator.MoveNext(out existing);
        if (!forced && existing != null)
        {
            mask = null;
            return false;
        }

        TryGetEntity(node, biome, (gridUid, grid), out var prototype);
        if (layer.EntityMask.Count > 0 &&
            (prototype == null || !layer.EntityMask.ContainsKey(prototype)) ||
            prototype != null && layer.Prototype != null)
        {
            mask = null;
            return false;
        }

        mask = prototype;
        return true;
    }

    private void CommitMarkerGeneration(MarkerGenerationJob job)
    {
        foreach (var ent in job.Existing)
            Del(ent);

        if (!job.Component.LoadedMarkers.TryGetValue(job.Layer, out var loaded))
        {
            loaded = new HashSet<Vector2i>();
            job.Component.LoadedMarkers[job.Layer] = loaded;
        }

        loaded.Add(job.Chunk);
        foreach (var (node, mask) in job.SpawnSet)
        {
            var origin = SharedMapSystem.GetChunkIndices(node, ChunkSize) * ChunkSize;
            if (!job.Component.PendingMarkers.TryGetValue(origin, out var layers))
            {
                layers = new Dictionary<string, List<Vector2i>>();
                job.Component.PendingMarkers[origin] = layers;
            }

            if (!layers.TryGetValue(job.Layer, out var nodes))
            {
                nodes = new List<Vector2i>();
                layers[job.Layer] = nodes;
            }

            nodes.Add(node);
        }
    }

    private sealed class MarkerGenerationJob : Job<object?>
    {
        private readonly BiomeSystem _system;
        private readonly BiomeComponent _biome;
        private readonly EntityUid _gridUid;
        private readonly MapGridComponent _grid;
        private readonly BiomeMarkerLayerPrototype _prototype;
        private readonly Box2i _bounds;
        private readonly int _count;
        private readonly RobustRandom _random = new();
        private readonly HashSet<Vector2i> _remainingTiles = new();
        private readonly Dictionary<Vector2i, EntityUid?> _entities = new();
        private readonly Dictionary<Vector2i, string?> _masks = new();
        private readonly List<Vector2i> _frontier = new();
        private int _x;
        private int _y;
        private int _group;
        private int _groupSize;
        private bool _scanning = true;

        public readonly BiomeComponent Component;
        public readonly EntityUid Grid;
        public readonly string Layer;
        public readonly Vector2i Chunk;
        public readonly bool Forced;
        public readonly Dictionary<Vector2i, string?> SpawnSet = new();
        public readonly HashSet<EntityUid> Existing = new();

        public MarkerGenerationJob(BiomeSystem system, BiomeComponent biome, EntityUid gridUid, MapGridComponent grid,
            string layer, BiomeMarkerLayerPrototype prototype, Vector2i chunk, Box2i bounds, int count, int seed,
            bool forced) : base(MarkerGenerationTime)
        {
            _system = system;
            _biome = biome;
            Component = biome;
            Grid = _gridUid = gridUid;
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
                var valid = _system.GetMarkerCandidate(_gridUid, _biome, _grid, _prototype, Forced, node,
                    out var existing, out var mask);
                if (valid)
                {
                    _remainingTiles.Add(node);
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

            while (_group < _count && _remainingTiles.Count > 0)
            {
                if (_groupSize == 0)
                {
                    _frontier.Clear();
                    _groupSize = _random.Next(_prototype.MinGroupSize, _prototype.MaxGroupSize + 1);
                }

                if (_frontier.Count == 0)
                {
                    var start = _random.Pick(_remainingTiles);
                    _remainingTiles.Remove(start);
                    _frontier.Add(start);
                }

                var index = _random.Next(_frontier.Count);
                var current = _frontier[index];
                _frontier.RemoveAt(index);
                _remainingTiles.Remove(current);
                SpawnSet[current] = _masks[current];
                if (_entities[current] is { } existing)
                    Existing.Add(existing);
                _groupSize--;
                if (_groupSize == 0)
                    _group++;

                for (var dx = -1; dx <= 1; dx++)
                for (var dy = -1; dy <= 1; dy++)
                {
                    var neighbor = new Vector2i(current.X + dx, current.Y + dy);
                    if (_frontier.Contains(neighbor) || !_remainingTiles.Contains(neighbor))
                        continue;
                    _frontier.Add(neighbor);
                }

                await SuspendIfOutOfTime();
            }

            return null;
        }
    }
}
