using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> LavalandEnabled =
        CVarDef.Create("lavaland.enabled", false, CVar.SERVERONLY);
}
