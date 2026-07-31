using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<float> ShuttleThermalDetectionMultiplier =
        CVarDef.Create("shuttle.detection.thermal_multiplier", 2f, CVar.ARCHIVE | CVar.REPLICATED);

    public static readonly CVarDef<float> ShuttleVisualDetectionMultiplier =
        CVarDef.Create("shuttle.detection.visual_multiplier", 16f, CVar.ARCHIVE | CVar.REPLICATED);
}
