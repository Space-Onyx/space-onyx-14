using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Sets maximum length for custom loadout item names.
    /// </summary>
    public static readonly CVarDef<int> MaxCustomLoadoutNameLength =
        CVarDef.Create("ic.custom_loadout_name_length", 100, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    ///     Sets maximum length for custom loadout item descriptions.
    /// </summary>
    public static readonly CVarDef<int> MaxCustomLoadoutDescriptionLength =
        CVarDef.Create("ic.custom_loadout_description_length", 512, CVar.SERVER | CVar.REPLICATED);
}
