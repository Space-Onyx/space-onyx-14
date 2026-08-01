using Content.Shared.Power.EntitySystems;
using JetBrains.Annotations;
using Robust.Shared.Utility;

namespace Content.Shared.Materials.OreSilo;

public abstract partial class SharedOreSiloSystem : EntitySystem
{
    [Dependency] private SharedMaterialStorageSystem _materialStorage = default!;
    [Dependency] private SharedPowerReceiverSystem _powerReceiver = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    [Dependency] private EntityQuery<OreSiloClientComponent> _clientQuery = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<OreSiloComponent, ToggleOreSiloClientMessage>(OnToggleOreSiloClient);
        SubscribeLocalEvent<OreSiloComponent, ComponentShutdown>(OnSiloShutdown);
        Subs.BuiEvents<OreSiloComponent>(OreSiloUiKey.Key,
            subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnBoundUIOpened);
        });


        SubscribeLocalEvent<OreSiloClientComponent, GetStoredMaterialsEvent>(OnGetStoredMaterials);
        SubscribeLocalEvent<OreSiloClientComponent, ConsumeStoredMaterialsEvent>(OnConsumeStoredMaterials);
        SubscribeLocalEvent<OreSiloClientComponent, ComponentShutdown>(OnClientShutdown);
    }

    private void OnToggleOreSiloClient(Entity<OreSiloComponent> ent, ref ToggleOreSiloClientMessage args)
    {
        var client = GetEntity(args.Client);

        if (!_clientQuery.TryComp(client, out _))
            return;

        // <Onyx-MaterialSiloDeviceLink-edited>
        // The silo UI only disconnects existing links. New links are created with a multitool.
        if (!ent.Comp.Clients.Contains(client))
            return;

        OnClientUiUnlinked(ent, client);
        UnlinkClient(ent, client);
        // </Onyx-MaterialSiloDeviceLink-edited>
    }

    // <Onyx-MaterialSiloDeviceLink>
    public bool TryLinkClient(Entity<OreSiloComponent> silo, EntityUid client)
    {
        if (!_clientQuery.TryComp(client, out var clientComp))
            return false;

        if (clientComp.Silo == silo.Owner)
            return true;

        if (clientComp.Silo != null || !CanTransmitMaterials((silo, silo), client, requirePower: false)) // <Onyx-MaterialSiloDeviceLink-edited>
            return false;

        var clientMats = _materialStorage.GetStoredMaterials(client, true);
        var inverseMats = new Dictionary<string, int>();
        foreach (var (mat, amount) in clientMats)
            inverseMats.Add(mat, -amount);

        _materialStorage.TryChangeMaterialAmount(client, inverseMats, localOnly: true);
        _materialStorage.TryChangeMaterialAmount((silo.Owner, (MaterialStorageComponent?) null), clientMats);

        silo.Comp.Clients.Add(client);
        Dirty(silo);
        clientComp.Silo = silo;
        Dirty(client, clientComp);
        UpdateOreSiloUi(silo);
        return true;
    }

    public bool UnlinkClient(Entity<OreSiloComponent> silo, EntityUid client)
    {
        if (!silo.Comp.Clients.Remove(client))
            return false;

        Dirty(silo);
        if (_clientQuery.TryComp(client, out var clientComp) && clientComp.Silo == silo.Owner)
        {
            clientComp.Silo = null;
            Dirty(client, clientComp);
        }

        UpdateOreSiloUi(silo);
        return true;
    }

    protected virtual void OnClientUiUnlinked(Entity<OreSiloComponent> silo, EntityUid client)
    {
    }
    // </Onyx-MaterialSiloDeviceLink>

    private void OnBoundUIOpened(Entity<OreSiloComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateOreSiloUi(ent);
    }

    private void OnSiloShutdown(Entity<OreSiloComponent> ent, ref ComponentShutdown args)
    {
        foreach (var client in ent.Comp.Clients)
        {
            if (!_clientQuery.TryComp(client, out var comp))
                continue;

            comp.Silo = null;
            Dirty(client, comp);
        }
    }

    protected virtual void UpdateOreSiloUi(Entity<OreSiloComponent> ent)
    {

    }

    private void OnGetStoredMaterials(Entity<OreSiloClientComponent> ent, ref GetStoredMaterialsEvent args)
    {
        if (args.LocalOnly)
            return;

        if (ent.Comp.Silo is not { } silo)
            return;

        if (!CanTransmitMaterials(silo, ent))
            return;

        var materials = _materialStorage.GetStoredMaterials(silo);

        foreach (var (mat, amount) in materials)
        {
            // Don't supply materials that they don't usually have access to.
            if (!_materialStorage.IsMaterialWhitelisted((args.Entity, args.Entity), mat))
                continue;

            var existing = args.Materials.GetOrNew(mat);
            args.Materials[mat] = existing + amount;
        }
    }

    private void OnConsumeStoredMaterials(Entity<OreSiloClientComponent> ent, ref ConsumeStoredMaterialsEvent args)
    {
        if (args.LocalOnly)
            return;

        if (ent.Comp.Silo is not { } silo || !TryComp<MaterialStorageComponent>(silo, out var materialStorage))
            return;

        if (!CanTransmitMaterials(silo, ent))
            return;

        foreach (var (mat, amount) in args.Materials)
        {
            if (!_materialStorage.TryChangeMaterialAmount(silo, mat, amount, materialStorage))
                continue;
            args.Materials[mat] = 0;
        }
    }

    private void OnClientShutdown(Entity<OreSiloClientComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<OreSiloComponent>(ent.Comp.Silo, out var silo))
            return;

        silo.Clients.Remove(ent);
        Dirty(ent.Comp.Silo.Value, silo);
        UpdateOreSiloUi((ent.Comp.Silo.Value, silo));
    }

    /// <summary>
    /// Checks if a given client fulfills the criteria to link/receive materials from an ore silo.
    /// </summary>
    [PublicAPI]
    public bool CanTransmitMaterials(Entity<OreSiloComponent?, TransformComponent?> silo, EntityUid client, bool requirePower = true) // <Onyx-MaterialSiloDeviceLink-edited>
    {
        if (!Resolve(silo, ref silo.Comp1, ref silo.Comp2))
            return false;

        if (requirePower && !_powerReceiver.IsPowered(silo.Owner)) // <Onyx-MaterialSiloDeviceLink-edited>
            return false;

        // <Onyx-MaterialSiloDeviceLink-edited>
        if (!_transform.GetMapCoordinates(silo.Owner).InRange(_transform.GetMapCoordinates(client), silo.Comp1.Range))
            return false;
        // </Onyx-MaterialSiloDeviceLink-edited>

        return true;
    }
}
