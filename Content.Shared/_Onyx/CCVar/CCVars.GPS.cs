using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<float> GpsUpdateRate =
        CVarDef.Create("gps.update_rate", 1f, CVar.SERVER | CVar.REPLICATED);
}
