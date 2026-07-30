using Content.Shared.Procedural;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Salvage.Procedural.Prototypes;

[Prototype]
public sealed partial class LavalandDungeonRuinPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public ProtoId<DungeonConfigPrototype> Config = default!;

    [DataField(required: true)]
    public Vector2i Boundary;

    [DataField]
    public int SpawnAttempts = 8;

    [DataField(required: true)]
    public int Priority;
}
