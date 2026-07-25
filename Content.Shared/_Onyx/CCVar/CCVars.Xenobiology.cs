using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> XenobiologyBreedingEnabled =
        CVarDef.Create("xenobiology.breeding.enabled", true, CVar.SERVER | CVar.ARCHIVE);

    public static readonly CVarDef<float> XenobiologyBreedingInterval =
        CVarDef.Create("xenobiology.breeding.interval", 1f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);
}
