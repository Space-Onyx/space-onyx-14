using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Disease;

[Prototype]
public sealed partial class DiseaseTypePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] private LocId Name { get; set; }
    [ViewVariables] public string LocalizedName => Loc.GetString(Name);
}
