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

    /// <summary>
    /// Включает push уведомлений о раундах (lobby/started/ended) в DiscordAuthBot.
    /// </summary>
    public static readonly CVarDef<bool> DiscordRoundBotApiEnabled =
        CVarDef.Create("discord.round_bot_api_enabled", false, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// URL endpoint уведомлений о раундах DiscordAuthBot (POST /api/v1/round/event).
    /// </summary>
    public static readonly CVarDef<string> DiscordRoundBotApiUrl =
        CVarDef.Create("discord.round_bot_api_url", string.Empty, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Bearer-токен уведомлений о раундах. Должен совпадать с BOT_API_TOKEN бота.
    /// </summary>
    public static readonly CVarDef<string> DiscordRoundBotApiToken =
        CVarDef.Create("discord.round_bot_api_token", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL | CVar.ARCHIVE);

    /// <summary>
    /// Таймаут запроса уведомлений о раундах в секундах.
    /// </summary>
    public static readonly CVarDef<int> DiscordRoundBotApiTimeoutSeconds =
        CVarDef.Create("discord.round_bot_api_timeout", 5, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Включает push уведомлений о банах в DiscordAuthBot.
    /// </summary>
    public static readonly CVarDef<bool> DiscordBanBotApiEnabled =
        CVarDef.Create("discord.ban_bot_api_enabled", false, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// URL endpoint уведомлений о банах DiscordAuthBot (POST /api/v1/ban/event).
    /// </summary>
    public static readonly CVarDef<string> DiscordBanBotApiUrl =
        CVarDef.Create("discord.ban_bot_api_url", string.Empty, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Bearer-токен уведомлений о банах. Должен совпадать с BOT_API_TOKEN бота.
    /// </summary>
    public static readonly CVarDef<string> DiscordBanBotApiToken =
        CVarDef.Create("discord.ban_bot_api_token", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL | CVar.ARCHIVE);

    /// <summary>
    /// Таймаут запроса уведомлений о банах в секундах.
    /// </summary>
    public static readonly CVarDef<int> DiscordBanBotApiTimeoutSeconds =
        CVarDef.Create("discord.ban_bot_api_timeout", 5, CVar.SERVERONLY | CVar.ARCHIVE);

}
