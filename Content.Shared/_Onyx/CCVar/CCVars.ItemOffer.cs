using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> ItemOfferCursorIndicator =
        CVarDef.Create("hud.item_offer_cursor_indicator", true, CVar.ARCHIVE | CVar.CLIENTONLY);
}
