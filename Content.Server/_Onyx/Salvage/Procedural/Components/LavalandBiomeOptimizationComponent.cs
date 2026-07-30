using Content.Shared.Parallax.Biomes.Markers;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Salvage.Procedural.Components;

[RegisterComponent]
public sealed partial class LavalandBiomeOptimizationComponent : Component
{
    [DataField]
    public int ChunkLoadsPerTick = 8;

    [DataField]
    public int ImmediateLoadRadius = 1;

    [DataField]
    public int MarkerChunksPerTick = 1;

    [DataField]
    public int MarkerLoadsPerTick = 2;

    [DataField]
    public int ChunkUnloadsPerTick = 2;

    [DataField]
    public float UnloadDelay = 10f;

    public Box2 WarmupArea;

    public readonly Queue<(ProtoId<BiomeMarkerLayerPrototype> Layer, Vector2i Chunk)> WarmupQueue = new();

    public readonly Dictionary<Vector2i, float> InactiveChunks = new();
}
