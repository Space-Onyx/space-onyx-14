using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Должно ли голосование за карту автоматически начинаться после перезапуска раунда.
    /// </summary>
    public static readonly CVarDef<bool> VoteMapAutoAfterRestart =
        CVarDef.Create("vote.map_auto_after_restart", false, CVar.SERVERONLY);

    /// <summary>
    ///     Должно ли голосование за режим автоматически начинаться после перезапуска раунда.
    /// </summary>
    public static readonly CVarDef<bool> VotePresetAutoAfterRestart =
        CVarDef.Create("vote.preset_auto_after_restart", false, CVar.SERVERONLY);

    /// <summary>
    ///     Задержка перед автоматическим стартом голосования за карту после рестарта, в секундах.
    /// </summary>
    public static readonly CVarDef<int> VoteMapAutoDelay =
        CVarDef.Create("vote.map_auto_delay", 4, CVar.SERVERONLY);

    /// <summary>
    ///     Задержка перед автоматическим стартом голосования за режим после рестарта, в секундах.
    /// </summary>
    public static readonly CVarDef<int> VotePresetAutoDelay =
        CVarDef.Create("vote.preset_auto_delay", 4, CVar.SERVERONLY);

    /// <summary>
    ///     Включено ли автоматическое голосование за дебаг СМЭСов.
    /// </summary>
    public static readonly CVarDef<bool> VoteSmesDebugEnabled =
        CVarDef.Create("vote.smes_debug_enabled", false, CVar.SERVERONLY);

    /// <summary>
    ///     Максимальное количество игроков при котором будет голосование за дебаг СМЭСов.
    /// </summary>
    public static readonly CVarDef<int> VoteSmesDebugMaxPlayers =
        CVarDef.Create("vote.smes_debug_max_players", 10, CVar.SERVERONLY);

    /// <summary>
    ///     Длительность голосования за дебаг СМЭСов.
    /// </summary>
    public static readonly CVarDef<int> VoteSmesDebugTimer =
        CVarDef.Create("vote.smes_debug_timer", 60, CVar.SERVERONLY);

    /// <summary>
    ///     Задержка перед автоматическим стартом голосования за дебаг СМЭСов, в секундах.
    /// </summary>
    public static readonly CVarDef<int> VoteSmesDebugDelay =
        CVarDef.Create("vote.smes_debug_delay", 2, CVar.SERVERONLY);
}