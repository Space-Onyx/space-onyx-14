using System;
using System.Diagnostics.CodeAnalysis;
using Content.Shared._Onyx.Discord;
using Robust.Shared.Network;

namespace Content.Client._Onyx.Discord;

public sealed partial class DiscordIdManager
{
    [Dependency] private IClientNetManager _netMgr = default!;

    private string? _discordId;
    private string? _discordUsername;
    private string? _linkCode;

    public event Action? DiscordInfoUpdated;

    public void Initialize()
    {
        _netMgr.RegisterNetMessage<MsgDiscordIdInfo>(OnDiscordIdInfo);
        _netMgr.RegisterNetMessage<MsgDiscordUnlinkRequest>();
    }

    private void OnDiscordIdInfo(MsgDiscordIdInfo msg)
    {
        _discordId = msg.DiscordId;
        _discordUsername = msg.DiscordUsername;
        _linkCode = msg.LinkCode;
        DiscordInfoUpdated?.Invoke();
    }

    public bool TryGetDiscordId([NotNullWhen(true)] out string? discordId)
    {
        discordId = _discordId;
        return discordId != null;
    }

    public bool TryGetDiscordUsername([NotNullWhen(true)] out string? username)
    {
        username = _discordUsername;
        return username != null;
    }

    public bool TryGetLinkCode([NotNullWhen(true)] out string? linkCode)
    {
        linkCode = _linkCode;
        return linkCode != null;
    }

    public void RequestUnlink()
    {
        if (!_netMgr.IsConnected)
            return;

        _netMgr.ClientSendMessage(new MsgDiscordUnlinkRequest());
    }

    public void RequestDiscordInfo()
    {
        if (!_netMgr.IsConnected)
            return;

        _netMgr.ClientSendMessage(new MsgDiscordIdInfo());
    }
}
