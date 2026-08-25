using System.Linq;
using Content.Server.Power.EntitySystems;
// <Onyx-ResearchNetworks>
using Content.Shared._Onyx.Research;
using Content.Shared.Examine;
// </Onyx-ResearchNetworks>
using Content.Shared.Research.Components;

namespace Content.Server.Research.Systems;

public sealed partial class ResearchSystem
{
    private const string ServerHashCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // <Onyx-ResearchNetworks>

    private void InitializeServer()
    {
        SubscribeLocalEvent<ResearchServerComponent, ComponentStartup>(OnServerStartup);
        SubscribeLocalEvent<ResearchServerComponent, ComponentShutdown>(OnServerShutdown);
        SubscribeLocalEvent<ResearchServerComponent, TechnologyDatabaseModifiedEvent>(OnServerDatabaseModified);
        SubscribeLocalEvent<ResearchServerComponent, ExaminedEvent>(OnServerExamined); // <Onyx-ResearchNetworks>
    }

    private void OnServerStartup(EntityUid uid, ResearchServerComponent component, ComponentStartup args)
    {
        var unusedId = EntityQuery<ResearchServerComponent>(true)
            .Max(s => s.Id) + 1;
        component.Id = unusedId;
        AssignServerIdentity(component); // <Onyx-ResearchNetworks>
        Dirty(uid, component);
        // <Onyx-ResearchNetworks>
        InitializeNetworkMember(uid, component);
        ReconcileNetwork(uid, component);
        LogNetworkEvent(uid, ResearchNetworkLogType.ServerOnline,
            Loc.GetString("research-network-log-server-online", ("server", component.ServerName), ("network", component.NetworkId)), component);
        // </Onyx-ResearchNetworks>
    }

    private void OnServerShutdown(EntityUid uid, ResearchServerComponent component, ComponentShutdown args)
    {
        // <Onyx-ResearchNetworks>
        var survivors = GetNetworkServers(uid, component);
        var replacement = survivors.FirstOrDefault();
        if (replacement.Owner != default)
        {
            LogNetworkEvent(replacement, ResearchNetworkLogType.ServerOffline,
                Loc.GetString("research-network-log-server-offline", ("server", component.ServerName), ("network", component.NetworkId)), replacement.Comp);
        }
        // </Onyx-ResearchNetworks>
        foreach (var client in new List<EntityUid>(component.Clients))
        {
            UnregisterClient(client, uid, serverComponent: component, dirtyServer: false);
            // <Onyx-ResearchNetworks>
            if (replacement.Owner != default)
                RegisterClient(client, replacement, serverComponent: replacement.Comp);
            // </Onyx-ResearchNetworks>
        }
    }

    private void OnServerDatabaseModified(EntityUid uid, ResearchServerComponent component, ref TechnologyDatabaseModifiedEvent args)
    {
        // <Onyx-ResearchNetworks-edited>
        if (!_synchronizingNetwork && TryGetNetworkAuthority(uid, out var authority, out var authorityComponent, component))
            SynchronizeNetwork(authority, authorityComponent);

        foreach (var client in GetNetworkClients(uid, component))
        {
            RaiseLocalEvent(client, ref args);
        }
        // </Onyx-ResearchNetworks-edited>
    }

    private bool CanRun(EntityUid uid)
    {
        return this.IsPowered(uid, EntityManager);
    }

    private void UpdateServer(EntityUid uid, int time, ResearchServerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!CanRun(uid))
            return;
        ModifyServerPoints(uid, GetPointsPerSecond(uid, component) * time, component);
    }

    /// <summary>
    /// Registers a client to the specified server.
    /// </summary>
    /// <param name="client">The client being registered</param>
    /// <param name="server">The server the client is being registered to</param>
    /// <param name="clientComponent"></param>
    /// <param name="serverComponent"></param>
    /// <param name="dirtyServer">Whether or not to dirty the server component after registration</param>
    public void RegisterClient(EntityUid client, EntityUid server, ResearchClientComponent? clientComponent = null,
        ResearchServerComponent? serverComponent = null,  bool dirtyServer = true)
    {
        if (!Resolve(client, ref clientComponent, false) || !Resolve(server, ref serverComponent, false))
            return;

        if (serverComponent.Clients.Contains(client))
            return;

        serverComponent.Clients.Add(client);
        clientComponent.Server = server;
        SyncClientWithServer(client, clientComponent: clientComponent);

        if (dirtyServer && !TerminatingOrDeleted(server))
            Dirty(server, serverComponent);

        var ev = new ResearchRegistrationChangedEvent(server);
        RaiseLocalEvent(client, ref ev);
    }

    /// <summary>
    /// Unregisterse a client from its server
    /// </summary>
    /// <param name="client"></param>
    /// <param name="clientComponent"></param>
    /// <param name="dirtyServer"></param>
    public void UnregisterClient(EntityUid client, ResearchClientComponent? clientComponent = null, bool dirtyServer = true)
    {
        if (!Resolve(client, ref clientComponent))
            return;

        if (clientComponent.Server is not { } server)
            return;

        UnregisterClient(client, server, clientComponent, dirtyServer: dirtyServer);
    }

    /// <summary>
    /// Unregisters a client from its server
    /// </summary>
    /// <param name="client"></param>
    /// <param name="server"></param>
    /// <param name="clientComponent"></param>
    /// <param name="serverComponent"></param>
    /// <param name="dirtyServer"></param>
    public void UnregisterClient(EntityUid client, EntityUid server, ResearchClientComponent? clientComponent = null,
        ResearchServerComponent? serverComponent = null, bool dirtyServer = true)
    {
        if (!Resolve(client, ref clientComponent, false) || !Resolve(server, ref serverComponent, false))
            return;

        serverComponent.Clients.Remove(client);
        clientComponent.Server = null;
        SyncClientWithServer(client, clientComponent: clientComponent);

        if (dirtyServer && !TerminatingOrDeleted(server))
        {
            Dirty(server, serverComponent);
        }

        var ev = new ResearchRegistrationChangedEvent(null);
        RaiseLocalEvent(client, ref ev);
    }

    /// <summary>
    /// Gets the amount of points generated by all the server's sources in a second.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    /// <returns></returns>
    public int GetPointsPerSecond(EntityUid uid, ResearchServerComponent? component = null)
    {
        var points = 0;

        if (!Resolve(uid, ref component))
            return points;

        if (!TryGetNetworkAuthority(uid, out var authority, out var authorityComponent, component) || authority != uid || !CanRun(uid)) // <Onyx-ResearchNetworks-edited>
            return points;

        // <Onyx-ResearchNetworks-edited>
        var sources = new HashSet<EntityUid>();
        foreach (var member in GetNetworkServers(uid, authorityComponent))
        {
            if (!member.Comp.GenerationEnabled)
                continue;

            sources.Add(member.Owner);
            foreach (var client in member.Comp.Clients)
                sources.Add(client);
        }

        var ev = new ResearchServerGetPointsPerSecondEvent(uid, points);
        foreach (var source in sources)
            RaiseLocalEvent(source, ref ev);
        // </Onyx-ResearchNetworks-edited>
        return ev.Points;
    }

    /// <summary>
    /// Adds a specified number of points to a server.
    /// </summary>
    /// <param name="uid">The server</param>
    /// <param name="points">The amount of points being added</param>
    /// <param name="component"></param>
    public void ModifyServerPoints(EntityUid uid, int points, ResearchServerComponent? component = null)
    {
        if (points == 0)
            return;

        // <Onyx-ResearchNetworks-edited>
        if (!Resolve(uid, ref component) ||
            !TryGetNetworkAuthority(uid, out var authority, out var authorityComponent, component))
            return;
        var previousPoints = authorityComponent.Points;
        authorityComponent.Points = Math.Max(0, authorityComponent.Points + points);
        var actualDelta = authorityComponent.Points - previousPoints;
        var ev = new ResearchServerPointsChangedEvent(authority, authorityComponent.Points, actualDelta);
        foreach (var client in GetNetworkClients(authority, authorityComponent))
        {
            RaiseLocalEvent(client, ref ev);
        }
        Dirty(authority, authorityComponent);
        SynchronizeNetwork(authority, authorityComponent);
        // </Onyx-ResearchNetworks-edited>
    }

    // <Onyx-ResearchNetworks>
    private void AssignServerIdentity(ResearchServerComponent component)
    {
        if (!string.IsNullOrEmpty(component.HashId))
            return;

        string hash;
        do
        {
            var chars = new char[6];
            for (var i = 0; i < chars.Length; i++)
                chars[i] = ServerHashCharacters[_random.Next(ServerHashCharacters.Length)];
            hash = new string(chars);
        } while (EntityQuery<ResearchServerComponent>(true).Any(server => server.HashId == hash));

        component.HashId = hash;
        if (string.IsNullOrWhiteSpace(component.ServerName) || component.ServerName is "RDSERVER" or "RND-Server")
            component.ServerName = $"RND-Server {hash}";
    }
    // </Onyx-ResearchNetworks>

    // <Onyx-ResearchNetworks>
    public string GetResearchLogUserName(EntityUid? user)
    {
        if (user is not { } uid ||
            !_idCard.TryFindIdCard(uid, out var idCard) ||
            string.IsNullOrWhiteSpace(idCard.Comp.FullName))
            return Loc.GetString("research-network-log-user-unknown");

        return string.IsNullOrWhiteSpace(idCard.Comp.LocalizedJobTitle)
            ? idCard.Comp.FullName
            : Loc.GetString("research-network-log-user-with-job",
                ("name", idCard.Comp.FullName),
                ("job", idCard.Comp.LocalizedJobTitle));
    }

    private void OnServerExamined(EntityUid uid, ResearchServerComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange ||
            !TryGetNetworkAuthority(uid, out var authority, out var authorityComponent, component))
            return;

        args.PushMarkup(Loc.GetString("research-server-network-examine",
            ("name", component.ServerName),
            ("hash", component.HashId),
            ("network", component.NetworkId),
            ("authority", authority == uid
                ? Loc.GetString("research-server-network-examine-authority")
                : Loc.GetString("research-server-network-examine-forwarded", ("hash", authorityComponent.HashId))),
            ("generation", GetServerGeneration(uid, component)),
            ("points", authorityComponent.Points),
            ("state", Loc.GetString(component.GenerationEnabled
                ? "research-server-control-state-enabled"
                : "research-server-control-state-disabled"))));
    }
    // </Onyx-ResearchNetworks>
}
