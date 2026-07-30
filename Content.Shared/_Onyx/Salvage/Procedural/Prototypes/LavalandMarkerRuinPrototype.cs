using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Salvage.Procedural.Prototypes;

[Prototype]
public sealed partial class LavalandMarkerRuinPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public Vector2i Boundary;

    [DataField(required: true)]
    public EntProtoId SpawnedMarker = default!;

    [DataField]
    public int SpawnAttempts = 8;

    [DataField(required: true)]
    public int Priority;
}
