// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Ghost.Skins;

/// <summary>
/// The client sends this to change their selected ghost skin.
/// The server validates the prototype and its requirements before saving.
/// </summary>
public sealed class MsgSelectGhostSkin : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public ProtoId<GhostSkinPrototype> Skin = GhostSkinPrototype.DefaultSkinId;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Skin = new ProtoId<GhostSkinPrototype>(buffer.ReadString());
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Skin.Id);
    }
}
