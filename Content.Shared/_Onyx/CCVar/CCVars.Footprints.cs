using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<float> MinimumPuddleSizeForFootprints =
        CVarDef.Create("footprints.minimum_puddle_size", 6f, CVar.SERVERONLY);
}
