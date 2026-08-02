using Content.Shared.Parallax.Biomes.Markers;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Salvage.Procedural.Components;

[RegisterComponent]
public sealed partial class LavalandBiomeWarmupComponent : Component
{
    public Box2 WarmupArea;

    public readonly Queue<(ProtoId<BiomeMarkerLayerPrototype> Layer, Vector2i Chunk)> WarmupQueue = new();
}
