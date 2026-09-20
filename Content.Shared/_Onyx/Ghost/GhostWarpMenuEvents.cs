// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Ghost;

[Serializable, NetSerializable]
public sealed class GhostWarpMenuRequestEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class GhostWarpMenuResponseEvent : EntityEventArgs
{
    public GhostWarpMenuResponseEvent(List<GhostWarpMenuEntry> entries)
    {
        Entries = entries;
    }

    public List<GhostWarpMenuEntry> Entries { get; }
}

[Serializable, NetSerializable]
public readonly struct GhostWarpMenuEntry
{
    public GhostWarpMenuEntry(
        NetEntity entity,
        string name,
        GhostWarpMenuCategory category,
        string? description = null,
        string? group = null,
        ProtoId<JobIconPrototype>? jobIcon = null)
    {
        Entity = entity;
        Name = name;
        Category = category;
        Description = description;
        Group = group;
        JobIcon = jobIcon;
    }

    public NetEntity Entity { get; }
    public string Name { get; }
    public GhostWarpMenuCategory Category { get; }
    public string? Description { get; }
    public string? Group { get; }
    public ProtoId<JobIconPrototype>? JobIcon { get; }
}

[Serializable, NetSerializable]
public enum GhostWarpMenuCategory : byte
{
    Antagonist,
    Alive,
    Ghost,
    Disconnected,
    Dead,
    Location,
}
