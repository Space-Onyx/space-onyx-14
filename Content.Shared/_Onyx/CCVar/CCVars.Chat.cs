using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> ChatLogActions =
        CVarDef.Create("chat.log_in_chat", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> ChatCoalesceIdenticalMessages =
        CVarDef.Create("chat.coalesce_identical_messages", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
