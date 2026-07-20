using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Disease;

[Prototype]
public sealed partial class DiseaseSpreadPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
}
