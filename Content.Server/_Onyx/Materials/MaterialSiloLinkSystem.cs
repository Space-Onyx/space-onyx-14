using Content.Server.DeviceLinking.Systems;
using Content.Shared._Onyx.Materials;
using Content.Shared._Onyx.Salvage.MiningPoints;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Materials;
using Content.Shared.Materials.OreSilo;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Server.Materials;

public sealed partial class OreSiloSystem
{
    [Dependency] private DeviceLinkSystem _deviceLink = default!;
    [Dependency] private MiningPointsSystem _miningPoints = default!;

    private static readonly ProtoId<SourcePortPrototype> MaterialSiloSourcePort = "MaterialSilo";
    private static readonly ProtoId<SinkPortPrototype> MaterialSiloSinkPort = "MaterialSiloUtilizer";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MaterialSiloLinkComponent, ComponentStartup>(OnMaterialSiloLinkStartup);
        SubscribeLocalEvent<MaterialSiloLinkComponent, MapInitEvent>(OnMaterialSiloLinkMapInit);
        SubscribeLocalEvent<MaterialSiloLinkComponent, LinkAttemptEvent>(OnMaterialSiloLinkAttempt);
        SubscribeLocalEvent<MaterialSiloLinkComponent, NewLinkEvent>(OnMaterialSiloNewLink);
        SubscribeLocalEvent<MaterialSiloLinkComponent, PortDisconnectedEvent>(OnMaterialSiloPortDisconnected);
        SubscribeLocalEvent<SalvageMiningPointProcessorComponent, MaterialEntityInsertedEvent>(OnMiningPointOreInserted);
    }

    private void OnMiningPointOreInserted(Entity<SalvageMiningPointProcessorComponent> ent, ref MaterialEntityInsertedEvent args)
    {
        if (!TryComp<OreSiloClientComponent>(ent, out var client) ||
            client.Silo is not { } siloUid ||
            !TryComp<OreSiloComponent>(siloUid, out var silo) ||
            !CanTransmitMaterials((siloUid, silo), ent))
        {
            return;
        }

        if (!TryComp<StackComponent>(args.Inserted, out var stack))
            return;

        var halfUnits = stack.StackTypeId.Id switch
        {
            "CoalUnprocessed" => 2,
            "SteelOre" => 6,
            "SpaceQuartz" => 7,
            "PlasmaOre" => 12,
            "GoldOre" => 24,
            "SilverOre" => 24,
            "UraniumOre" => 48,
            "BananiumOre" => 138,
            "BSCrystalUnprocessed" => 500,
            "DiamondOre" => 2000,
            _ => 0,
        };

        if (TryComp<MiningPointsComponent>(ent, out var points))
            _miningPoints.AddHalfUnits((ent.Owner, points), (long) halfUnits * args.Count);
    }

    private void OnMaterialSiloLinkStartup(Entity<MaterialSiloLinkComponent> ent, ref ComponentStartup args)
    {
        if (HasComp<OreSiloComponent>(ent))
            _deviceLink.EnsureSourcePorts(ent, MaterialSiloSourcePort);

        if (HasComp<OreSiloClientComponent>(ent))
            _deviceLink.EnsureSinkPorts(ent, MaterialSiloSinkPort);
    }

    private void OnMaterialSiloLinkMapInit(Entity<MaterialSiloLinkComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<OreSiloComponent>(ent, out var silo))
            return;

        foreach (var client in _deviceLink.GetLinkedSinks((ent.Owner, (DeviceLinkSourceComponent?) null), MaterialSiloSourcePort))
            TryLinkClient((ent, silo), client);
    }

    private void OnMaterialSiloLinkAttempt(Entity<MaterialSiloLinkComponent> ent, ref LinkAttemptEvent args)
    {
        if (args.SourcePort != MaterialSiloSourcePort || args.SinkPort != MaterialSiloSinkPort)
            return;

        if (!HasComp<OreSiloComponent>(args.Source) ||
            !TryComp<OreSiloClientComponent>(args.Sink, out var client) ||
            client.Silo is { } silo && silo != args.Source ||
            !CanTransmitMaterials(args.Source, args.Sink))
        {
            args.Cancel();
        }
    }

    private void OnMaterialSiloNewLink(Entity<MaterialSiloLinkComponent> ent, ref NewLinkEvent args)
    {
        if (ent.Owner != args.Source ||
            args.SourcePort != MaterialSiloSourcePort ||
            args.SinkPort != MaterialSiloSinkPort ||
            !TryComp<OreSiloComponent>(ent, out var silo))
        {
            return;
        }

        if (!TryLinkClient((ent, silo), args.Sink))
            _deviceLink.RemoveSinkFromSource(ent, args.Sink);
    }

    private void OnMaterialSiloPortDisconnected(Entity<MaterialSiloLinkComponent> ent, ref PortDisconnectedEvent args)
    {
        if (args.Port != MaterialSiloSinkPort ||
            !TryComp<OreSiloClientComponent>(ent, out var client) ||
            client.Silo is not { } siloUid ||
            !TryComp<OreSiloComponent>(siloUid, out var silo))
        {
            return;
        }

        UnlinkClient((siloUid, silo), ent);
    }

    protected override void OnClientUiUnlinked(Entity<OreSiloComponent> silo, EntityUid client)
    {
        _deviceLink.RemoveSinkFromSource(silo, client);
    }
}
