using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> SpaceWhaleSpawn =
        CVarDef.Create("misc.space_whale_spawn", true, CVar.SERVER);

    public static readonly CVarDef<int> SpaceWhaleSpawnDistance =
        CVarDef.Create("misc.space_whale_spawn_distance", 1965, CVar.SERVER);
}
