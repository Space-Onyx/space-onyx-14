// Content taken from Wega (https://github.com/wega-team/ss14-wega), licensed under GNU GPL v3.
using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<int> MaxOocFlavorTextLength =
        CVarDef.Create("ic.ooc_flavor_text_length", 2048, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<int> MaxCharacterFlavorTextLength =
        CVarDef.Create("ic.character_flavor_text_length", 2048, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<int> MaxFlavorTagsLength =
        CVarDef.Create("ic.flavor_tags_length", 128, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<int> MaxFlavorLinksLength =
        CVarDef.Create("ic.flavor_links_length", 512, CVar.SERVER | CVar.REPLICATED);
}
