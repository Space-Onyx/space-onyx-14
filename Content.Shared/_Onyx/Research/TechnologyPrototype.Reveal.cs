using Robust.Shared.Prototypes;

namespace Content.Shared.Research.Prototypes;

public sealed partial class TechnologyPrototype
{
    /// <summary>
    /// Destructive-analysis requirements that reveal a hidden technology.
    /// </summary>
    [DataField]
    public List<ResearchItemRequirement> RevealRequirements = new();

    /// <summary>
    /// Destructive-analysis requirements that allow a technology to be researched.
    /// </summary>
    [DataField]
    public List<ResearchItemRequirement> ResearchRequirements = new();
}

[DataDefinition]
public sealed partial class ResearchItemRequirement
{
    [DataField(required: true)]
    public List<EntProtoId> AnyOf = new();

    [DataField]
    public int Amount = 1;
}
