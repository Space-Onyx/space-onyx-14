using Content.Shared._Onyx.Research;

namespace Content.Shared.Xenoarchaeology.Artifact.Components;

public sealed partial class XenoArtifactNodeComponent
{
    /// <summary>
    /// Typed research point rewards granted once when this node's value is extracted.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ResearchPointAmount> TypedPointRewards = new();

    /// <summary>
    /// When true and <see cref="TypedPointRewards"/> is empty, seeds an Experimental
    /// reward from CCVars.ArtifactNodeExperimentalReward on map init.
    /// </summary>
    [DataField]
    public bool AutoTypedPointRewards = true;
}
