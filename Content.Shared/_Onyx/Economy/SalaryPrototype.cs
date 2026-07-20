using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Economy;

[Prototype]
public sealed partial class SalaryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; set; } = default!;

    [DataField("salaries")]
    public Dictionary<string, int> Salaries = new();
}
