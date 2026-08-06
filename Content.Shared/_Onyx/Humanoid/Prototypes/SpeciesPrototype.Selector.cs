using Robust.Shared.Utility;

namespace Content.Shared.Humanoid.Prototypes;

public sealed partial class SpeciesPrototype
{
    [DataField]
    public SpeciesCategory Category = SpeciesCategory.Classic;

    [DataField]
    public ResPath? Description;

    [DataField]
    public List<string> Pros = new();

    [DataField]
    public List<string> Cons = new();

    [DataField]
    public List<string> Special = new();
}

public enum SpeciesCategory : byte
{
    Classic,
    Unusual,
    Special,
}
