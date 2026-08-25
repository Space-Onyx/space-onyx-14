using Content.Shared._Onyx.Research;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Research.Components;

/// <summary>
/// Marks an item as a research sample that can be destructively analyzed
/// for typed point rewards using the listed analysis methods.
/// </summary>
[RegisterComponent]
public sealed partial class ResearchAnalyzableComponent : Component
{
    /// <summary>
    /// Points awarded per analysis method. Method ids are free-form and localized
    /// via the research-machine-destructive-method-{id} locale keys.
    /// </summary>
    [DataField]
    public Dictionary<string, List<ResearchPointAmount>> MethodPointRewards = new();

    /// <summary>
    /// Display order of methods. Defaults to the reward dictionary order.
    /// </summary>
    [DataField]
    public List<string> SupportedMethods = new();

    /// <summary>
    /// Technologies added to the connected server's database on a successful analysis.
    /// </summary>
    [DataField]
    public List<ProtoId<TechnologyPrototype>> UnlockTechnologies = new();

    /// <summary>
    /// Hidden technologies revealed (made visible and purchasable) on a successful analysis.
    /// </summary>
    [DataField]
    public List<ProtoId<TechnologyPrototype>> RevealTechnologies = new();
}
