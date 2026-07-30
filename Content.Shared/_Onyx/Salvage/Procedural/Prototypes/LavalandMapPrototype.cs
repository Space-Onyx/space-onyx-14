using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Salvage.Procedural.Prototypes;

[Prototype]
public sealed partial class LavalandMapPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public ProtoId<LavalandPlanetPrototype> Planet = default!;

    [DataField(required: true)]
    public ProtoId<LavalandLayoutPrototype> Layout = default!;

    [DataField(required: true)]
    public ProtoId<LavalandRuinPoolPrototype> Ruins = default!;
}
