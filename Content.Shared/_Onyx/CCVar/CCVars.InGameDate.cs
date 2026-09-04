using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<int> InGameYearOffset =
        CVarDef.Create("game.in_game_year_offset", 500, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);
}
