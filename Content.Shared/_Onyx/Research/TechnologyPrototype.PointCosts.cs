using Content.Shared._Onyx.Research;
using Content.Shared.Research.Prototypes;

namespace Content.Shared.Research.Prototypes;

public sealed partial class TechnologyPrototype
{
    /// <summary>
    /// Typed point costs. When empty the legacy <see cref="Cost"/> paid in General points is used.
    /// </summary>
    [DataField]
    public List<ResearchPointAmount> PointCosts = new();
}
