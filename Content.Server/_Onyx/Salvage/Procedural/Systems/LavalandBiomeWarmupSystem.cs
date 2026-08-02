using Content.Server._Onyx.Salvage.Procedural.Components;
using Content.Server.Parallax;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax.Biomes.Markers;
using Robust.Shared.Map.Enumerators;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Salvage.Procedural.Systems;

public sealed partial class LavalandBiomeWarmupSystem : EntitySystem
{
    [Dependency] private BiomeSystem _biome = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

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

        var chunks = new List<(ProtoId<BiomeMarkerLayerPrototype> Layer, Vector2i Chunk, float Distance)>();
        foreach (var layer in biome.MarkerLayers)
        {
            var prototype = _prototypes.Index(layer);
            var enumerator = new ChunkIndicesEnumerator(ent.Comp.WarmupArea, prototype.Size);
            while (enumerator.MoveNext(out var chunk))
            {
                var origin = (chunk * prototype.Size).Value;
                var nearestX = Math.Max(0, Math.Max(origin.X, -origin.X - prototype.Size));
                var nearestY = Math.Max(0, Math.Max(origin.Y, -origin.Y - prototype.Size));
                chunks.Add((layer, origin, nearestX * nearestX + nearestY * nearestY));
            }
        }

        chunks.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        foreach (var chunk in chunks)
            ent.Comp.WarmupQueue.Enqueue((chunk.Layer, chunk.Chunk));
    }

    public override void Update(float frameTime)
    {
        var query = AllEntityQuery<LavalandBiomeWarmupComponent, BiomeComponent>();
        while (query.MoveNext(out var uid, out var optimization, out var biome))
        {
            while (optimization.WarmupQueue.TryPeek(out var entry))
            {
                if (_biome.IsMarkerChunkLoaded(biome, entry.Layer, entry.Chunk))
                {
                    optimization.WarmupQueue.Dequeue();
                    continue;
                }

                _biome.PreloadMarkerChunk(uid, biome, entry.Layer, entry.Chunk);
                break;
            }
        }
    }
}
