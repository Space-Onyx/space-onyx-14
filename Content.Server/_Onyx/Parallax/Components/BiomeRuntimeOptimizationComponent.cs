namespace Content.Server._Onyx.Parallax.Components;

[RegisterComponent]
public sealed partial class BiomeRuntimeOptimizationComponent : Component
{
    [DataField]
    public int ChunkLoadsPerTick = 1;

    [DataField]
    public int MarkerChunksPerTick = 1;

    [DataField]
    public int MarkerLoadsPerTick = 1;

    [DataField]
    public int MarkerNodesPerTick = 4;

    [DataField]
    public int ChunkUnloadsPerTick = 2;

    [DataField]
    public float UnloadDelay = 10f;

    public readonly Dictionary<Vector2i, float> InactiveChunks = new();
}
