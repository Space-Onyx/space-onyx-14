using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<float> CosmicCultistDifficultyMultiplier =
        CVarDef.Create("cosmiccult.difficulty_multiplier", 1.5f, CVar.SERVER);

    public static readonly CVarDef<int> CosmicCultistEntropyValue =
        CVarDef.Create("cosmiccult.cultist_entropy_value", 5, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<int> CosmicCultTargetConversionPercent =
        CVarDef.Create("cosmiccult.target_conversion_percent", 40, CVar.SERVER);

    public static readonly CVarDef<int> CosmicCultStewardVoteTimer =
        CVarDef.Create("cosmiccult.steward_vote_timer", 120, CVar.SERVER);

    public static readonly CVarDef<int> CosmicCultT2RevealDelaySeconds =
        CVarDef.Create("cosmiccult.t2_reveal_delay_seconds", 120, CVar.SERVER);

    public static readonly CVarDef<int> CosmicCultT3RevealDelaySeconds =
        CVarDef.Create("cosmiccult.t3_reveal_delay_seconds", 60, CVar.SERVER);

    public static readonly CVarDef<int> CosmicCultFinaleDelaySeconds =
        CVarDef.Create("cosmiccult.extra_entropy_for_finale", 150, CVar.SERVER);
}
