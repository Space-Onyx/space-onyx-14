using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> WoundsBleedingAutoStopEnabled =
        CVarDef.Create("wounds.bleeding_auto_stop_enabled", true, CVar.SERVER | CVar.ARCHIVE);

    public static readonly CVarDef<float> WoundsBleedingAutoStopSecondsPerSeverity =
        CVarDef.Create("wounds.bleeding_auto_stop_seconds_per_severity", 2f, CVar.SERVER | CVar.ARCHIVE);

    public static readonly CVarDef<float> WoundsBleedingAutoStopMinSeconds =
        CVarDef.Create("wounds.bleeding_auto_stop_min_seconds", 5f, CVar.SERVER | CVar.ARCHIVE);

    public static readonly CVarDef<float> WoundsBleedingAutoStopMaxSeconds =
        CVarDef.Create("wounds.bleeding_auto_stop_max_seconds", 120f, CVar.SERVER | CVar.ARCHIVE);
}
