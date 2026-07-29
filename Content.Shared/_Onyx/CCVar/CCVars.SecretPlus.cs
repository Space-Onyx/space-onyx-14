using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<float> StationEventSpeedup =
        CVarDef.Create("stationevents.debug_speedup", 1f, CVar.SERVERONLY);

    public static readonly CVarDef<int> StationEventPlayerBias =
        CVarDef.Create("stationevents.debug_player_bias", 0, CVar.SERVERONLY);

    public static readonly CVarDef<float> MinimumTimeUntilFirstEvent =
        CVarDef.Create("gamedirector.minimumtimeuntilfirstevent", 300f, CVar.SERVERONLY);

    public static readonly CVarDef<float> RoundstartChaosScoreMultiplier =
        CVarDef.Create("gamedirector.roundstart_chaos_score_multiplier", 1f, CVar.SERVERONLY);
}
