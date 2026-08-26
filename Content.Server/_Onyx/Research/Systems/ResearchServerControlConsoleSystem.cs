using System.Linq;
using Content.Server.Power.EntitySystems;
using Content.Server.Research.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared._Onyx.Research;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Research.Systems;

public sealed partial class ResearchServerControlConsoleSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private ResearchSystem _research = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        SubscribeLocalEvent<ResearchServerControlConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<ResearchServerControlConsoleComponent, ToggleResearchServerGenerationMessage>(OnToggleGeneration);
        SubscribeLocalEvent<ResearchServerControlConsoleComponent, SetResearchServerNetworkMessage>(OnSetNetwork);
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextUpdate)
            return;
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);

        var query = EntityQueryEnumerator<ResearchServerControlConsoleComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (_ui.IsUiOpen(uid, ResearchServerControlUiKey.Key))
                UpdateUi((uid, component));
        }
    }

    private void OnUiOpened(Entity<ResearchServerControlConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnToggleGeneration(Entity<ResearchServerControlConsoleComponent> ent, ref ToggleResearchServerGenerationMessage args)
    {
        if (!_ui.IsUiOpen(ent.Owner, ResearchServerControlUiKey.Key, args.Actor) ||
            !this.IsPowered(ent.Owner, EntityManager) ||
            TryComp<AccessReaderComponent>(ent, out var access) && !_access.IsAllowed(args.Actor, ent, access) ||
            !_research.TryGetServerById(ent, args.ServerId, out var server, out _))
            return;

        if (_research.TryToggleServerGeneration(server.Value, args.Actor))
            UpdateUi(ent);
    }

    private void OnSetNetwork(Entity<ResearchServerControlConsoleComponent> ent, ref SetResearchServerNetworkMessage args)
    {
        if (!CanControlServer(ent, args.Actor, args.ServerId, out var server))
            return;

        if (_research.TrySetServerNetwork(server, args.NetworkId, args.Actor))
            UpdateUi(ent);
    }

    private bool CanControlServer(
        Entity<ResearchServerControlConsoleComponent> ent,
        EntityUid actor,
        int serverId,
        out EntityUid server)
    {
        server = default;
        if (!_ui.IsUiOpen(ent.Owner, ResearchServerControlUiKey.Key, actor) ||
            !this.IsPowered(ent.Owner, EntityManager) ||
            TryComp<AccessReaderComponent>(ent, out var access) && !_access.IsAllowed(actor, ent, access) ||
            !_research.TryGetServerById(ent, serverId, out var found, out _))
            return false;

        server = found.Value;
        return true;
    }

    private void UpdateUi(Entity<ResearchServerControlConsoleComponent> ent)
    {
        var servers = _research.GetServers(ent)
            .OrderBy(server => server.Comp.NetworkId, StringComparer.Ordinal)
            .ThenBy(server => server.Comp.Id)
            .ToList();
        var entries = new List<ResearchServerControlEntry>(servers.Count);
        var logs = new List<ResearchNetworkLogEntry>();

        foreach (var server in servers)
        {
            if (!_research.TryGetNetworkAuthority(server, out var authority, out var authorityComponent, server.Comp))
                continue;

            entries.Add(new ResearchServerControlEntry(
                server.Comp.Id,
                server.Comp.HashId,
                server.Comp.ServerName,
                server.Comp.NetworkId,
                this.IsPowered(server, EntityManager),
                authority == server.Owner,
                authorityComponent.Id,
                authorityComponent.HashId,
                server.Comp.GenerationEnabled,
                _research.GetServerGenerationByType(server, server.Comp),
                new(authorityComponent.PointBalances)));

            if (authority == server.Owner)
                logs.AddRange(authorityComponent.NetworkLogs);
        }

        logs.Sort((left, right) => right.Timestamp.CompareTo(left.Timestamp));
        _ui.SetUiState(ent.Owner, ResearchServerControlUiKey.Key,
            new ResearchServerControlBoundInterfaceState(entries, logs));
    }
}
