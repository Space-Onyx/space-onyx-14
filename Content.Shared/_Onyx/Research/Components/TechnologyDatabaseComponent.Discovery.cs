using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Research.Components;

public sealed partial class TechnologyDatabaseComponent
{
    /// <summary>
    /// Hidden technologies that have been revealed (e.g. by destructive analysis)
    /// and are now visible and purchasable at the console.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<TechnologyPrototype>> RevealedTechnologies = new();

    /// <summary>
    /// Completed reveal requirement indices, keyed by technology id.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<TechnologyPrototype>, Dictionary<int, int>> CompletedRevealRequirements = new();

    /// <summary>
    /// Completed research requirement indices, keyed by technology id.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<TechnologyPrototype>, Dictionary<int, int>> CompletedResearchRequirements = new();

}
