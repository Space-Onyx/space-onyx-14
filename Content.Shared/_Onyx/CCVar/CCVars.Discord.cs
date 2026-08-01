using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Вебхук для уведомлений о серверных банах.
    /// </summary>
    public static readonly CVarDef<string> DiscordBanWebhook =
        CVarDef.Create("discord.ban_webhook", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    /// Ссылка на Discord-канал с инструкциями по привязке аккаунта.
    /// </summary>
    public static readonly CVarDef<string> DiscordLinkChannel =
        CVarDef.Create("discord.link_channel", string.Empty, CVar.REPLICATED | CVar.ARCHIVE);

    /// <summary>
    /// Токен Discord-бота для получения данных Discord-пользователей.
    /// </summary>
    public static readonly CVarDef<string> DiscordTokenBot =
        CVarDef.Create("discord.token_bot", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL | CVar.ARCHIVE);

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

    /// <summary>
    /// URL API DiscordAuthBot для глобальной отвязки аккаунта.
    /// </summary>
    public static readonly CVarDef<string> DiscordAuthBotApiUrl =
        CVarDef.Create("discord.auth_bot_api_url", string.Empty, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Bearer-токен API DiscordAuthBot.
    /// </summary>
    public static readonly CVarDef<string> DiscordAuthBotApiToken =
        CVarDef.Create("discord.auth_bot_api_token", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL | CVar.ARCHIVE);

    /// <summary>
    /// Таймаут запроса к API DiscordAuthBot в секундах.
    /// </summary>
    public static readonly CVarDef<int> DiscordAuthBotApiTimeoutSeconds =
        CVarDef.Create("discord.auth_bot_api_timeout", 5, CVar.SERVERONLY | CVar.ARCHIVE);

}
