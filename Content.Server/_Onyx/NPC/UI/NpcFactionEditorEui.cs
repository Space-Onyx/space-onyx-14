using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Content.Shared._Onyx.NPC;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.NPC.UI;

[UsedImplicitly]
public sealed partial class NpcFactionEditorEui : BaseEui
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    private readonly NpcFactionSystem _faction;

    private readonly EntityUid _target;

    public NpcFactionEditorEui(EntityUid target)
    {
        IoCManager.InjectDependencies(this);
        _target = target;
        _faction = _entManager.System<NpcFactionSystem>();
    }

    public override void Opened()
    {
        base.Opened();
        _admin.OnPermsChanged += OnPermsChanged;
        StateDirty();
    }

    public override void Closed()
    {
        base.Closed();
        _admin.OnPermsChanged -= OnPermsChanged;
    }

    public override EuiStateBase GetNewState()
    {
        var allFactions = _prototype.EnumeratePrototypes<NpcFactionPrototype>()
            .Select(p => p.ID)
            .OrderBy(id => id)
            .ToList();

        if (!_entManager.EntityExists(_target))
            return BuildEmptyState(allFactions);

        _entManager.TryGetComponent(_target, out MetaDataComponent? meta);
        var name = meta?.EntityName ?? "<deleted>";
        if (!_entManager.TryGetComponent(_target, out NpcFactionMemberComponent? comp))
            return new NpcFactionEditorEuiState(
                _entManager.GetNetEntity(_target),
                name,
                allFactions,
                new List<string>(),
                new List<string>(),
                new List<string>(),
                new List<string>(),
                new List<string>());

        return new NpcFactionEditorEuiState(
            _entManager.GetNetEntity(_target),
            name,
            allFactions,
            comp.Factions.Select(static f => f.ToString()).OrderBy(static x => x).ToList(),
            comp.AddFriendlyFactions?.Select(static f => f.ToString()).OrderBy(static x => x).ToList() ?? new List<string>(),
            comp.AddHostileFactions?.Select(static f => f.ToString()).OrderBy(static x => x).ToList() ?? new List<string>(),
            comp.FriendlyFactions.Select(static f => f.ToString()).OrderBy(static x => x).ToList(),
            comp.HostileFactions.Select(static f => f.ToString()).OrderBy(static x => x).ToList());
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (!CanUse())
        {
            Close();
            return;
        }

        if (msg is not NpcFactionEditorEuiMsg.ApplyChanges apply)
            return;

        if (!_entManager.TryGetComponent(_target, out NpcFactionMemberComponent? comp))
            return;

        var ent = (_target, comp);

        _faction.ClearFactions(ent, dirty: false);
        foreach (var faction in ToFactionSet(apply.Factions))
        {
            _faction.AddFaction(ent, faction, dirty: false);
        }

        _faction.ClearFriendlyFactions(ent, dirty: false);
        foreach (var faction in ToFactionSet(apply.FriendlyOverrides))
        {
            _faction.AddFriendlyFaction(ent, faction, dirty: false);
        }

        _faction.ClearHostileFactions(ent, dirty: false);
        foreach (var faction in ToFactionSet(apply.HostileOverrides))
        {
            _faction.AddHostileFaction(ent, faction, dirty: false);
        }

        _faction.RefreshFactionCache(ent);
        _entManager.Dirty(_target, comp);
        StateDirty();
    }

    private HashSet<string> ToFactionSet(IEnumerable<string> values)
    {
        var set = new HashSet<string>();
        foreach (var value in values)
        {
            var trimmed = value.Trim();
            if (trimmed.Length == 0 || !_prototype.HasIndex<NpcFactionPrototype>(trimmed))
                continue;

            set.Add(trimmed);
        }

        return set;
    }

    private bool CanUse()
    {
        return _admin.HasAdminFlag(Player, AdminFlags.Debug);
    }

    private void OnPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player != Player)
            return;

        if (!CanUse())
            Close();
    }

    private NpcFactionEditorEuiState BuildEmptyState(List<string> allFactions)
    {
        return new NpcFactionEditorEuiState(
            NetEntity.Invalid,
            "<deleted>",
            allFactions,
            new List<string>(),
            new List<string>(),
            new List<string>(),
            new List<string>(),
            new List<string>());
    }
}
