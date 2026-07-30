using Content.Shared._Onyx.Salvage.Procedural.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Maps;

public sealed partial class GameMapPrototype
{
    [DataField]
    public List<ProtoId<LavalandMapPrototype>> Planets = new() { "Lavaland" };
}
