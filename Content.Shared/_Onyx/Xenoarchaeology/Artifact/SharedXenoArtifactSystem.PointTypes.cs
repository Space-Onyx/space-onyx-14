using Content.Shared.CCVar;
using Content.Shared._Onyx.Research;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Robust.Shared.Configuration;

namespace Content.Shared.Xenoarchaeology.Artifact;

public abstract partial class SharedXenoArtifactSystem
{
    [Dependency] private IConfigurationManager _pointTypesConfig = default!;

    /// <summary>
    /// Seeds the node's typed point rewards from CCVars unless the prototype defines its own.
    /// Called from the shared node map-init handler.
    /// </summary>
    private void SeedNodeTypedPointRewards(Entity<XenoArtifactNodeComponent> ent)
    {
        if (!ent.Comp.AutoTypedPointRewards || GetNodeTypedPointRewards(ent).Count > 0)
            return;

        SeedNodeTypedPointReward(ent,
            new ResearchPointAmount("Experimental", _pointTypesConfig.GetCVar(CCVars.ArtifactNodeExperimentalReward)));
    }
}
