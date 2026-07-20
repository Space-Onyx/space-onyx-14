using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Вебхук для уведомлений о серверных банах.
    /// </summary>
    public static readonly CVarDef<string> DiscordBansWebhook =
        CVarDef.Create("discord.bans_webhook", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    /// Ссылка на Discord-канал с инструкциями по привязке аккаунта.
    /// </summary>
    public static readonly CVarDef<string> DiscordLinkChannel =
        CVarDef.Create("discord.link_channel", string.Empty, CVar.REPLICATED | CVar.ARCHIVE);

    /// <summary>
    /// Включает или отключает систему привязки аккаунта через Discord.
    /// </summary>
    public static readonly CVarDef<bool> DiscordAuthEnable =
        CVarDef.Create("discord.auth_enable", false, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /// <summary>
    /// Если true, привязка Discord-аккаунта обязательна для входа.
    /// </summary>
    public static readonly CVarDef<bool> DiscordAuthLinkRequired =
        CVarDef.Create("discord.auth_link_required", false, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

}
