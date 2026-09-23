using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<int> InGameYearOffset =
        CVarDef.Create("game.in_game_year_offset", 500, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<bool> RandomizeStationTime =
        CVarDef.Create("game.randomize_station_time", false, CVar.SERVER | CVar.ARCHIVE);

    public static readonly CVarDef<bool> UseRealStationTime =
        CVarDef.Create("game.use_real_station_time", true, CVar.SERVER | CVar.ARCHIVE);
}
