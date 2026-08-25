using Robust.Shared.Prototypes;

namespace Content.Shared.Research.Prototypes;

public sealed partial class TechnologyPrototype
{
    /// <summary>
    /// Item prototypes that reveal this hidden technology when destructively analyzed.
    /// </summary>
    [DataField]
    public List<EntProtoId> RequiredItemsToUnlock = new();
}
