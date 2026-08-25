using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Power.EntitySystems;
using Content.Shared.Research.Components;
using Robust.Shared.Utility;

namespace Content.Server.Research.Systems;

public sealed partial class ResearchSystem
{
    private void InitializeClient()
    {
        SubscribeLocalEvent<ResearchClientComponent, MapInitEvent>(OnClientMapInit);
        SubscribeLocalEvent<ResearchClientComponent, ComponentShutdown>(OnClientShutdown);
        SubscribeLocalEvent<ResearchClientComponent, BoundUIOpenedEvent>(OnClientUIOpen);
        SubscribeLocalEvent<ResearchClientComponent, ConsoleServerSelectionMessage>(OnConsoleSelect);
        SubscribeLocalEvent<ResearchClientComponent, AnchorStateChangedEvent>(OnClientAnchorStateChanged);

        SubscribeLocalEvent<ResearchClientComponent, ResearchClientSyncMessage>(OnClientSyncMessage);
        SubscribeLocalEvent<ResearchClientComponent, ResearchClientServerSelectedMessage>(OnClientSelected);
        SubscribeLocalEvent<ResearchClientComponent, ResearchClientServerDeselectedMessage>(OnClientDeselected);
        SubscribeLocalEvent<ResearchClientComponent, ResearchRegistrationChangedEvent>(OnClientRegistrationChanged);
    }

    #region UI

    private void OnClientSelected(EntityUid uid, ResearchClientComponent component, ResearchClientServerSelectedMessage args)
    {
        if (!TryGetServerById(uid, args.ServerId, out var serveruid, out var serverComponent))
            return;

        // <Onyx-ResearchNetworks>
        if (!TryGetNetworkAuthority(serveruid.Value, out var authority, out _, serverComponent) || authority != serveruid.Value)
            return;
        // </Onyx-ResearchNetworks>

        // Validate that we can access this server.
        if (!GetServers(uid).Contains((serveruid.Value, serverComponent)))
            return;

        UnregisterClient(uid, component);
        RegisterClient(uid, serveruid.Value, component, serverComponent);
    }

    private void OnClientDeselected(EntityUid uid, ResearchClientComponent component, ResearchClientServerDeselectedMessage args)
    {
        UnregisterClient(uid, clientComponent: component);
    }

    private void OnClientSyncMessage(EntityUid uid, ResearchClientComponent component, ResearchClientSyncMessage args)
    {
        UpdateClientInterface(uid, component);
    }

    private void OnConsoleSelect(EntityUid uid, ResearchClientComponent component, ConsoleServerSelectionMessage args)
    {
        if (!this.IsPowered(uid, EntityManager))
            return;

        _uiSystem.TryToggleUi(uid, ResearchClientUiKey.Key, args.Actor);
    }
    #endregion

    private void OnClientRegistrationChanged(EntityUid uid, ResearchClientComponent component, ref ResearchRegistrationChangedEvent args)
    {
        UpdateClientInterface(uid, component);
    }

    private void OnClientMapInit(EntityUid uid, ResearchClientComponent component, MapInitEvent args)
    {
        // <Onyx-ResearchNetworks-edited>
        var server = GetSelectableServers(uid).FirstOrDefault();
        if (server.Owner != default)
            RegisterClient(uid, server, component, server);
        // </Onyx-ResearchNetworks-edited>
    }

    private void OnClientShutdown(EntityUid uid, ResearchClientComponent component, ComponentShutdown args)
    {
        UnregisterClient(uid, component);
    }

    private void OnClientUIOpen(EntityUid uid, ResearchClientComponent component, BoundUIOpenedEvent args)
    {
        UpdateClientInterface(uid, component);
    }

    private void OnClientAnchorStateChanged(Entity<ResearchClientComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
        {
            if (ent.Comp.Server is not null)
                return;

            // <Onyx-ResearchNetworks-edited>
            var server = GetSelectableServers(ent).FirstOrDefault();
            if (server.Owner != default)
                RegisterClient(ent, server, ent, server);
            // </Onyx-ResearchNetworks-edited>
        }
        else
        {
            UnregisterClient(ent, ent.Comp);
        }
    }

    // <Onyx-ResearchNetworks>
    private List<Entity<ResearchServerComponent>> GetSelectableServers(EntityUid client)
    {
        return GetServers(client)
            .Where(server => TryGetNetworkAuthority(server, out var authority, out _, server.Comp) && authority == server.Owner)
            .OrderBy(server => server.Comp.Id)
            .ToList();
    }
    // </Onyx-ResearchNetworks>

    private void UpdateClientInterface(EntityUid uid, ResearchClientComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        TryGetClientServer(uid, out _, out var serverComponent, component);

        // <Onyx-ResearchNetworks-edited>
        var servers = GetServers(uid).OrderBy(server => server.Comp.Id).ToArray();
        var names = servers.Select(server => server.Comp.ServerName).ToArray();
        var hashIds = servers.Select(server => server.Comp.HashId).ToArray();
        var networkIds = servers.Select(server => server.Comp.NetworkId).ToArray();
        var authorities = new bool[servers.Length];
        var authorityIds = new int[servers.Length];
        for (var i = 0; i < servers.Length; i++)
        {
            if (!TryGetNetworkAuthority(servers[i], out var authority, out var authorityComponent, servers[i].Comp))
                continue;
            authorities[i] = authority == servers[i].Owner;
            authorityIds[i] = authorityComponent.Id;
        }

        var selectedServerId = component.Server is { } selected && TryComp<ResearchServerComponent>(selected, out var selectedComponent)
            ? selectedComponent.Id
            : -1;
        var state = new ResearchClientBoundInterfaceState(
            names.Length,
            names,
            servers.Select(server => server.Comp.Id).ToArray(),
            hashIds,
            networkIds,
            authorities,
            authorityIds,
            selectedServerId);
        // </Onyx-ResearchNetworks-edited>

        _uiSystem.SetUiState(uid, ResearchClientUiKey.Key, state);
    }

    /// <summary>
    /// Tries to get the server belonging to a client
    /// </summary>
    /// <param name="uid">The client</param>
    /// <param name="server">It's server. Null if false.</param>
    /// <param name="serverComponent">The server's ResearchServerComponent. Null if false</param>
    /// <param name="component">The client's Researchclient component</param>
    /// <returns>If the server was successfully retrieved.</returns>
    public bool TryGetClientServer(EntityUid uid,
        [NotNullWhen(returnValue: true)] out EntityUid? server,
        [NotNullWhen(returnValue: true)] out ResearchServerComponent? serverComponent,
        ResearchClientComponent? component = null)
    {
        server = null;
        serverComponent = null;

        if (!Resolve(uid, ref component, false))
            return false;

        if (component.Server == null)
            return false;

        // <Onyx-ResearchNetworks-edited>
        if (!TryComp<ResearchServerComponent>(component.Server, out var selectedComponent) ||
            !TryGetNetworkAuthority(component.Server.Value, out var authority, out serverComponent, selectedComponent))
            return false;

        server = authority;
        // </Onyx-ResearchNetworks-edited>
        return true;
    }
}
