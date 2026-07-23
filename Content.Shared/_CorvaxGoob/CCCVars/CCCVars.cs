using Robust.Shared.Configuration;

namespace Content.Shared._CorvaxGoob.CCCVars;

[CVarDefs]
public sealed class CCCVars
{
    public static readonly CVarDef<bool> PhotoPlayTimeRequire =
        CVarDef.Create("photo.playtime_require", true, CVar.SERVERONLY);

    public static readonly CVarDef<float> PhotoPlayTimeHours =
        CVarDef.Create("photo.playtime_require_time", 20f, CVar.SERVERONLY);
}
