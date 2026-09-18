// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.NanoChat;

/// <summary>
/// Summary of a group chat stored on a NanoChat card.
/// Group identifiers live in a reserved range above regular NanoChat numbers,
/// so group and direct chats can share the same message store.
/// </summary>
[Serializable, NetSerializable, DataRecord]
public partial struct NanoChatGroup
{
    /// <summary>
    /// Reserved identifier range for group chats. Regular NanoChat numbers never reach it.
    /// </summary>
    public const uint FirstGroupId = 10000;

    /// <summary>
    /// Unique group identifier.
    /// </summary>
    public uint Id;

    /// <summary>
    /// Displayed group name.
    /// </summary>
    public string Name;

    /// <summary>
    /// Amount of cards currently inside the group.
    /// </summary>
    public int MemberCount;

    /// <summary>
    /// Whether the group has unread messages.
    /// </summary>
    public bool HasUnread;

    public NanoChatGroup(uint id, string name, int memberCount = 1, bool hasUnread = false)
    {
        Id = id;
        Name = name;
        MemberCount = memberCount;
        HasUnread = hasUnread;
    }
}
