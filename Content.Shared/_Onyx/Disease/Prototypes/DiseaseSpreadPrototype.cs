using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Disease;

[Prototype]
public sealed partial class DiseaseSpreadPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [DataField] public bool BlockedByInternals;
}
