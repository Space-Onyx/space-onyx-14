using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Discord;

public sealed class MsgDiscordIdInfo : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.EntityEvent;

    public NetUserId UserId;
    public string? DiscordId;
    public string? DiscordUsername;
    public string? LinkCode;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        UserId = new NetUserId(buffer.ReadGuid());
        DiscordId = buffer.ReadBoolean() ? buffer.ReadString() : null;
        DiscordUsername = buffer.ReadBoolean() ? buffer.ReadString() : null;
        LinkCode = buffer.ReadBoolean() ? buffer.ReadString() : null;
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(UserId.UserId);
        buffer.Write(DiscordId != null);
        if (DiscordId != null)
            buffer.Write(DiscordId);
        buffer.Write(DiscordUsername != null);
        if (DiscordUsername != null)
            buffer.Write(DiscordUsername);
        buffer.Write(LinkCode != null);
        if (LinkCode != null)
            buffer.Write(LinkCode);
    }
}
