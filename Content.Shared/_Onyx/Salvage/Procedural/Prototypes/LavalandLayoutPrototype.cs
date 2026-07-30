using Content.Shared._Onyx.Salvage.Procedural;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Salvage.Procedural.Prototypes;

[Prototype]
public sealed partial class LavalandLayoutPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<LavalandLayoutEntry> Layouts = new();
}
