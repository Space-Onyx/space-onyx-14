using System.Linq;
using Content.Shared._Onyx.Research;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;

namespace Content.Server.Research.Systems;

public sealed partial class ResearchSystem
{
    private bool _synchronizingNetwork;

    public void ReconcileDatabasesAfterPrototypeReload()
    {
        var query = EntityQueryEnumerator<TechnologyDatabaseComponent>();
        while (query.MoveNext(out var uid, out var database))
            ReconcileDatabaseAfterPrototypeReload(uid, database);
    }

    public void ReconcileDatabaseAfterPrototypeReload(EntityUid uid, TechnologyDatabaseComponent database)
    {
        database.UnlockedTechnologies.RemoveAll(id =>
            !ProtoMan.TryIndex<TechnologyPrototype>(id, out var technology) || technology.EditorDeleted);
        database.UnlockedRecipes.Clear();
        foreach (var id in database.UnlockedTechnologies)
        {
            if (!ProtoMan.TryIndex<TechnologyPrototype>(id, out var technology))
                continue;
            foreach (var recipe in technology.RecipeUnlocks)
            {
                if (!database.UnlockedRecipes.Contains(recipe))
                    database.UnlockedRecipes.Add(recipe);
            }
        }

        foreach (var technology in ProtoMan.EnumeratePrototypes<TechnologyPrototype>())
        {
            if (technology.EditorDeleted || !technology.StartingTechnology ||
                !database.SupportedDisciplines.Contains(technology.Discipline) ||
                database.UnlockedTechnologies.Contains(technology.ID))
                continue;

            database.UnlockedTechnologies.Add(technology.ID);
            foreach (var recipe in technology.RecipeUnlocks)
            {
                if (!database.UnlockedRecipes.Contains(recipe))
                    database.UnlockedRecipes.Add(recipe);
            }
        }

        Dirty(uid, database);
        UpdateTechnologyCards(uid, database);
    }

    public List<Entity<ResearchServerComponent>> GetNetworkServers(EntityUid server, ResearchServerComponent? component = null)
    {
        if (!Resolve(server, ref component, false) || Transform(server).GridUid is not { } grid)
            return new();

        return GetServers(server)
            .Where(other => !TerminatingOrDeleted(other.Owner) &&
                            string.Equals(other.Comp.NetworkId, component.NetworkId, StringComparison.Ordinal) &&
                            Transform(other).GridUid == grid)
            .OrderBy(other => other.Comp.Id)
            .ToList();
    }

    public bool TryGetNetworkAuthority(
        EntityUid server,
        out EntityUid authority,
        out ResearchServerComponent authorityComponent,
        ResearchServerComponent? component = null)
    {
        authority = default;
        authorityComponent = default!;
        var servers = GetNetworkServers(server, component);
        var first = servers.FirstOrDefault(member => CanRun(member));
        if (first.Owner == default)
            first = servers.FirstOrDefault();
        if (first.Owner == default)
            return false;

        authority = first.Owner;
        authorityComponent = first.Comp;
        return true;
    }

    private bool IsNetworkAuthority(EntityUid server, ResearchServerComponent component)
    {
        return TryGetNetworkAuthority(server, out var authority, out _, component) && authority == server;
    }

    private HashSet<EntityUid> GetNetworkClients(EntityUid server, ResearchServerComponent? component = null)
    {
        var clients = new HashSet<EntityUid>();
        foreach (var member in GetNetworkServers(server, component))
        {
            foreach (var client in member.Comp.Clients)
                clients.Add(client);
        }

        return clients;
    }

    public void MoveNetworkClientsToAuthority(EntityUid authority, ResearchServerComponent authorityComponent)
    {
        var authorityChanged = false;
        foreach (var member in GetNetworkServers(authority, authorityComponent))
        {
            if (member.Owner == authority)
                continue;

            var memberChanged = false;
            foreach (var client in member.Comp.Clients.ToArray())
            {
                if (!TryComp<ResearchClientComponent>(client, out var clientComponent))
                    continue;

                member.Comp.Clients.Remove(client);
                if (!authorityComponent.Clients.Contains(client))
                    authorityComponent.Clients.Add(client);
                authorityChanged = true;
                memberChanged = true;
                clientComponent.Server = authority;
                SyncClientWithServer(client, clientComponent: clientComponent);
                var registration = new ResearchRegistrationChangedEvent(authority);
                RaiseLocalEvent(client, ref registration);
            }

            if (memberChanged)
                Dirty(member.Owner, member.Comp);
        }

        if (authorityChanged)
            Dirty(authority, authorityComponent);
    }

    private void SynchronizeNetwork(EntityUid authority, ResearchServerComponent authorityComponent)
    {
        if (_synchronizingNetwork || !TryComp<TechnologyDatabaseComponent>(authority, out var authorityDatabase))
            return;

        _synchronizingNetwork = true;
        try
        {
            foreach (var member in GetNetworkServers(authority, authorityComponent))
            {
                if (member.Owner == authority)
                    continue;

                member.Comp.Points = authorityComponent.Points;
                member.Comp.PointBalances = new(authorityComponent.PointBalances);
                member.Comp.NextUpdateTime = authorityComponent.NextUpdateTime;
                member.Comp.NetworkLogs = new(authorityComponent.NetworkLogs);
                Dirty(member.Owner, member.Comp);

                if (TryComp<TechnologyDatabaseComponent>(member, out var memberDatabase))
                    CopyDatabase(member.Owner, memberDatabase, authorityDatabase);
            }
        }
        finally
        {
            _synchronizingNetwork = false;
        }

        foreach (var client in GetNetworkClients(authority, authorityComponent))
            SyncClientWithServer(client);
    }

    private void CopyDatabase(EntityUid target, TechnologyDatabaseComponent targetDatabase, TechnologyDatabaseComponent sourceDatabase)
    {
        targetDatabase.MainDiscipline = sourceDatabase.MainDiscipline;
        targetDatabase.CurrentTechnologyCards = new(sourceDatabase.CurrentTechnologyCards);
        targetDatabase.SupportedDisciplines = new(sourceDatabase.SupportedDisciplines);
        targetDatabase.UnlockedTechnologies = new(sourceDatabase.UnlockedTechnologies);
        targetDatabase.RevealedTechnologies = new(sourceDatabase.RevealedTechnologies);
        targetDatabase.UnlockedRecipes = new(sourceDatabase.UnlockedRecipes);
        Dirty(target, targetDatabase);
    }

    public void LogNetworkEvent(
        EntityUid server,
        ResearchNetworkLogType type,
        string message,
        ResearchServerComponent? component = null)
    {
        if (!Resolve(server, ref component, false) ||
            !TryGetNetworkAuthority(server, out var authority, out var authorityComponent, component))
            return;

        authorityComponent.NetworkLogs.Add(new ResearchNetworkLogEntry
        {
            Timestamp = _timing.CurTime,
            Type = type,
            Message = message,
        });

        while (authorityComponent.NetworkLogs.Count > ResearchServerComponent.MaxNetworkLogs)
            authorityComponent.NetworkLogs.RemoveAt(0);

        Dirty(authority, authorityComponent);
        SynchronizeNetwork(authority, authorityComponent);
    }

    private void ReconcileNetwork(EntityUid server, ResearchServerComponent component)
    {
        if (!TryGetNetworkAuthority(server, out var authority, out var authorityComponent, component))
            return;

        if (authority != server)
        {
            component.Points = authorityComponent.Points;
            component.PointBalances = new(authorityComponent.PointBalances);
            component.NetworkLogs = new(authorityComponent.NetworkLogs);
            Dirty(server, component);

            if (TryComp<TechnologyDatabaseComponent>(server, out var database) &&
                TryComp<TechnologyDatabaseComponent>(authority, out var authorityDatabase))
                CopyDatabase(server, database, authorityDatabase);
        }
    }

    public void InitializeNetworkMember(EntityUid server, ResearchServerComponent component)
    {
        var source = GetNetworkServers(server, component).FirstOrDefault(member => member.Owner != server);
        if (source.Owner == default)
            return;

        component.Points = source.Comp.Points;
        component.PointBalances = new(source.Comp.PointBalances);
        component.NextUpdateTime = source.Comp.NextUpdateTime;
        component.NetworkLogs = new(source.Comp.NetworkLogs);
        Dirty(server, component);

        if (TryComp<TechnologyDatabaseComponent>(server, out var database) &&
            TryComp<TechnologyDatabaseComponent>(source, out var sourceDatabase))
            CopyDatabase(server, database, sourceDatabase);
    }

    public int GetServerGeneration(EntityUid server, ResearchServerComponent? component = null)
    {
        if (!Resolve(server, ref component, false) || !component.GenerationEnabled ||
            !TryGetNetworkAuthority(server, out var authority, out _, component))
            return 0;

        var ev = new ResearchServerGetPointsPerSecondEvent(authority, 0);
        RaiseLocalEvent(server, ref ev);
        foreach (var client in component.Clients)
            RaiseLocalEvent(client, ref ev);
        return ev.Points;
    }

    public bool TryToggleServerGeneration(EntityUid server, EntityUid actor)
    {
        if (!TryComp<ResearchServerComponent>(server, out var component))
            return false;

        component.GenerationEnabled = !component.GenerationEnabled;
        Dirty(server, component);
        LogNetworkEvent(server, ResearchNetworkLogType.GenerationToggled,
            Loc.GetString("research-network-log-generation-toggled",
                ("server", component.ServerName),
                ("state", Loc.GetString(component.GenerationEnabled
                    ? "research-server-control-state-enabled"
                    : "research-server-control-state-disabled")),
                ("user", GetResearchLogUserName(actor))), component);
        return true;
    }

    public bool TrySetServerNetwork(EntityUid server, string networkId, EntityUid actor)
    {
        if (!TryComp<ResearchServerComponent>(server, out var component))
            return false;

        networkId = networkId.Trim();
        if (!IsValidNetworkId(networkId) || string.Equals(component.NetworkId, networkId, StringComparison.Ordinal))
            return false;

        var previousNetwork = component.NetworkId;
        var previousMembers = GetNetworkServers(server, component);
        var targetExists = GetServers(server).Any(other => other.Owner != server &&
            string.Equals(other.Comp.NetworkId, networkId, StringComparison.Ordinal));

        if (previousMembers.Count > 1)
        {
            var previousAuthority = previousMembers.FirstOrDefault(member => member.Owner != server && CanRun(member));
            if (previousAuthority.Owner == default)
                previousAuthority = previousMembers.First(member => member.Owner != server);
            MoveClients(server, component, previousAuthority.Owner, previousAuthority.Comp);
        }

        LogNetworkEvent(server, ResearchNetworkLogType.NetworkChanged,
            Loc.GetString("research-network-log-network-left",
                ("server", component.ServerName),
                ("network", previousNetwork),
                ("user", GetResearchLogUserName(actor))), component);

        component.NetworkId = networkId;
        Dirty(server, component);

        if (targetExists)
        {
            InitializeNetworkMember(server, component);
        }
        else if (previousMembers.Count > 1)
        {
            ResetNetworkMember(server, component);
        }

        ReconcileNetwork(server, component);
        LogNetworkEvent(server, ResearchNetworkLogType.NetworkChanged,
            Loc.GetString("research-network-log-network-changed",
                ("server", component.ServerName),
                ("oldNetwork", previousNetwork),
                ("newNetwork", networkId),
                ("user", GetResearchLogUserName(actor))), component);

        foreach (var client in component.Clients)
            SyncClientWithServer(client);
        return true;
    }

    private void MoveClients(
        EntityUid source,
        ResearchServerComponent sourceComponent,
        EntityUid target,
        ResearchServerComponent targetComponent)
    {
        foreach (var client in sourceComponent.Clients.ToArray())
        {
            if (!TryComp<ResearchClientComponent>(client, out var clientComponent))
                continue;

            sourceComponent.Clients.Remove(client);
            if (!targetComponent.Clients.Contains(client))
                targetComponent.Clients.Add(client);
            clientComponent.Server = target;
            SyncClientWithServer(client, clientComponent: clientComponent);
            var registration = new ResearchRegistrationChangedEvent(target);
            RaiseLocalEvent(client, ref registration);
        }

        Dirty(source, sourceComponent);
        Dirty(target, targetComponent);
    }

    private void ResetNetworkMember(EntityUid server, ResearchServerComponent component)
    {
        component.Points = 0;
        component.PointBalances = new() { new(ResearchPointAmount.General, 0) };
        component.NetworkLogs.Clear();
        component.NextUpdateTime = _timing.CurTime + component.ResearchConsoleUpdateTime;
        Dirty(server, component);

        if (!TryComp<TechnologyDatabaseComponent>(server, out var database))
            return;

        database.MainDiscipline = null;
        database.CurrentTechnologyCards.Clear();
        database.UnlockedTechnologies.Clear();
        database.RevealedTechnologies.Clear();
        database.UnlockedRecipes.Clear();
        UpdateTechnologyCards(server, database);
        Dirty(server, database);
    }

    private static bool IsValidNetworkId(string networkId)
    {
        if (networkId.Length is < 1 or > 24)
            return false;

        foreach (var character in networkId)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_')
                return false;
        }

        return true;
    }

    public bool TryRemoveNetworkTechnology(EntityUid server, string technology)
    {
        if (!TryGetNetworkAuthority(server, out var authority, out var authorityComponent) ||
            !TryComp<TechnologyDatabaseComponent>(authority, out var database) ||
            !ProtoMan.TryIndex<Content.Shared.Research.Prototypes.TechnologyPrototype>(technology, out var prototype) ||
            !TryRemoveTechnology((authority, database), prototype))
            return false;

        SynchronizeNetwork(authority, authorityComponent);
        var ev = new TechnologyDatabaseModifiedEvent(null);
        RaiseLocalEvent(authority, ref ev);
        return true;
    }
}
