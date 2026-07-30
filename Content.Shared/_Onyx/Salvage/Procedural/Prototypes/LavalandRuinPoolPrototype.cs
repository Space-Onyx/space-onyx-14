using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Salvage.Procedural.Prototypes;

[Prototype]
public sealed partial class LavalandRuinPoolPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public int RuinDistance = 14;

    [DataField]
    public int MaxDistance = 172;

    [DataField]
    public Dictionary<ProtoId<LavalandGridRuinPrototype>, ushort> GridRuins = new();

    [DataField]
    public Dictionary<ProtoId<LavalandDungeonRuinPrototype>, ushort> DungeonRuins = new();

    [DataField]
    public Dictionary<ProtoId<LavalandMarkerRuinPrototype>, ushort> MarkerRuins = new();
}
