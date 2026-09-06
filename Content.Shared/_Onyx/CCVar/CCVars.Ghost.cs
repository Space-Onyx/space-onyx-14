using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Включает кнопку возврата госта в лобби.
    /// </summary>
    public static readonly CVarDef<bool> GhostReturnToLobbyEnabled =
        CVarDef.Create("ghost.return_to_lobby_enabled", true, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /// <summary>
    /// Кулдаун перед тем, как гост  сможет вернуться в лобби (в секундах).
    /// </summary>
    public static readonly CVarDef<int> GhostReturnToLobbyDelay =
        CVarDef.Create("ghost.return_to_lobby_delay", 300, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Минимальное общее наигранное время для возврата госта в лобби (в часах).
    /// </summary>
    public static readonly CVarDef<int> GhostReturnToLobbyMinOverallHours =
        CVarDef.Create("ghost.return_to_lobby_min_overall_hours", 5, CVar.SERVER | CVar.ARCHIVE);
}
