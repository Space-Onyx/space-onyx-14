using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.Research.Prototypes;

/// <summary>
/// A type of research points used by the research network economy.
/// </summary>
[Prototype]
public sealed partial class ResearchPointTypePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Player facing name of the point type.
    /// </summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Color used by interfaces displaying balances and costs.
    /// </summary>
    [DataField]
    public Color Color = Color.White;
}
