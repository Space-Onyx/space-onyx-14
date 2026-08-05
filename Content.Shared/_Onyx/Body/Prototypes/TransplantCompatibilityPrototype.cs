using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Body.Prototypes;

[Prototype]
public sealed partial class TransplantCompatibilityPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public HashSet<string> Provides = [];

    [DataField]
    public HashSet<string> Accepts = [];
}
