using System;
using System.Collections.Generic;
using Content.Shared.Eui;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.NPC;

[Serializable, NetSerializable]
public sealed class NpcFactionEditorEuiState : EuiStateBase
{
    public readonly NetEntity Target;
    public readonly string EntityName;
    public readonly List<string> AllFactions;
    public readonly List<string> Factions;
    public readonly List<string> FriendlyOverrides;
    public readonly List<string> HostileOverrides;
    public readonly List<string> EffectiveFriendly;
    public readonly List<string> EffectiveHostile;

    public NpcFactionEditorEuiState(
        NetEntity target,
        string entityName,
        List<string> allFactions,
        List<string> factions,
        List<string> friendlyOverrides,
        List<string> hostileOverrides,
        List<string> effectiveFriendly,
        List<string> effectiveHostile)
    {
        Target = target;
        EntityName = entityName;
        AllFactions = allFactions;
        Factions = factions;
        FriendlyOverrides = friendlyOverrides;
        HostileOverrides = hostileOverrides;
        EffectiveFriendly = effectiveFriendly;
        EffectiveHostile = effectiveHostile;
    }
}

public static class NpcFactionEditorEuiMsg
{
    [Serializable, NetSerializable]
    public sealed class ApplyChanges : EuiMessageBase
    {
        public readonly List<string> Factions;
        public readonly List<string> FriendlyOverrides;
        public readonly List<string> HostileOverrides;

        public ApplyChanges(List<string> factions, List<string> friendlyOverrides, List<string> hostileOverrides)
        {
            Factions = factions;
            FriendlyOverrides = friendlyOverrides;
            HostileOverrides = hostileOverrides;
        }
    }
}
