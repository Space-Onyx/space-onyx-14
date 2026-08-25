using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<int> ArtifactNodeExperimentalReward =
        CVarDef.Create("research.artifact_node_experimental_reward", 110, CVar.SERVER | CVar.REPLICATED);
}
