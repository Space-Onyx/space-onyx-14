using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared.CCVar;
using Content.Shared._Onyx.Discord;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Server._Onyx.Discord;

public sealed partial class ServerDiscordIdManager : EntitySystem
{
    private static readonly TimeSpan LinkCodeLifetime = TimeSpan.FromMinutes(5);

    [Dependency] private IServerNetManager _net = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();
        _net.RegisterNetMessage<MsgDiscordIdInfo>(OnDiscordIdInfoRequest);
        _net.RegisterNetMessage<MsgDiscordUnlinkRequest>(OnDiscordUnlinkRequest);
    }

    private async void OnDiscordIdInfoRequest(MsgDiscordIdInfo msg)
    {
        await SendDiscordInfo(msg.MsgChannel);
    }

    private async Task SendDiscordInfo(INetChannel channel)
    {
        var userId = channel.UserId;
        var discordId = _cfg.GetCVar(CCVars.DiscordAuthEnable)
            ? await _db.GetDiscordIdAsync(userId.UserId)
            : null;
        string? linkCode = null;

        if (discordId == null && _cfg.GetCVar(CCVars.DiscordAuthEnable) &&
            _players.TryGetSessionById(userId, out var session))
        {
            linkCode = await _db.GetOrCreateDiscordLinkCodeAsync(userId.UserId, session.Name, LinkCodeLifetime);
        }
        else if (discordId != null)
        {
            await _db.RemoveDiscordLinkCodeAsync(userId.UserId);
        }

        _net.ServerSendMessage(new MsgDiscordIdInfo
        {
            UserId = userId,
            DiscordId = discordId,
            DiscordUsername = null,
            LinkCode = linkCode
        }, channel);
    }

    private async void OnDiscordUnlinkRequest(MsgDiscordUnlinkRequest msg)
    {
        if (_cfg.GetCVar(CCVars.DiscordAuthEnable))
            await _db.UnlinkDiscordIdAsync(msg.MsgChannel.UserId.UserId);
        await SendDiscordInfo(msg.MsgChannel);
    }
}
