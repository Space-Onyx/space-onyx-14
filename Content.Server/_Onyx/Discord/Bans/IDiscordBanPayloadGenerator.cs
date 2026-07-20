using Content.Server.Discord;

namespace Content.Server._Onyx.Discord.Bans;

public interface IDiscordBanPayloadGenerator
{
    WebhookPayload Generate(BanInfo info);
}
