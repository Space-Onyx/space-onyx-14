using System.Threading.Tasks;

namespace Content.Server._Onyx.Discord.Bans;

public interface IDiscordBanInfoSender
{
    Task SendBanInfoAsync<TGenerator>(BanInfo info)
        where TGenerator : IDiscordBanPayloadGenerator, new();
}
