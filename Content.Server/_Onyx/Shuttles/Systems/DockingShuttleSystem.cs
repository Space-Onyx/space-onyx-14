using Content.Server._Onyx.Salvage.DeathRattle;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Onyx.Shuttles.Components;
using Content.Shared._Onyx.Shuttles.Systems;
using Content.Shared.Shuttles.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Onyx.Shuttles.Systems;

public sealed partial class DockingShuttleSystem : SharedDockingShuttleSystem
{
    [Dependency] private DockingConsoleSystem _console = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private ShuttleConsoleSystem _shuttleConsole = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DockingShuttleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DockingShuttleComponent, FTLStartedEvent>(OnFTLStarted);
        SubscribeLocalEvent<DockingShuttleComponent, FTLCompletedEvent>(OnFTLCompleted);
        SubscribeLocalEvent<DockingShuttleComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<DockingShuttleComponent, ShuttleAddStationEvent>(OnAddStation);
        SubscribeLocalEvent<StationGridAddedEvent>(OnStationGridAdded);
        _shuttleConsole.FtlDestinationsChanged += RefreshAllDestinations;
    }

    private void OnMapInit(Entity<DockingShuttleComponent> ent, ref MapInitEvent args)
    {
        RefreshDestinations(ent);
        RefreshConsoles();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _shuttleConsole.FtlDestinationsChanged -= RefreshAllDestinations;
    }

    public void RefreshAllDestinations()
    {
        var query = EntityQueryEnumerator<DockingShuttleComponent>();
        while (query.MoveNext(out var uid, out var comp))
            RefreshDestinations((uid, comp));
    }

    internal static void SetStationDestination(DockingShuttleComponent shuttle, EntityUid station, MapId mapId)
    {
        shuttle.Station = station;
        shuttle.StationMap = mapId;
    }

    private void RefreshDestinations(Entity<DockingShuttleComponent> ent)
    {
        ent.Comp.Destinations.Clear();

        var query = EntityQueryEnumerator<FTLDestinationComponent, MapComponent>();
        while (query.MoveNext(out var mapUid, out var destination, out var map))
        {
            if (!destination.Enabled || _whitelist.IsWhitelistFailOrNull(destination.Whitelist, ent))
                continue;

            AddDestination(ent.Comp, Name(mapUid), map.MapId);
        }

        if (HasComp<MiningShuttleComponent>(ent))
        {
            var lavalandQuery = EntityQueryEnumerator<LavalandMapComponent, MapComponent>();
            while (lavalandQuery.MoveNext(out var mapUid, out _, out var map))
                AddDestination(ent.Comp, Name(mapUid), map.MapId);
        }

        if (ent.Comp.Station is { } station && ent.Comp.StationMap is { } stationMap && Exists(station))
            AddDestination(ent.Comp, Name(station), stationMap);

        _console.UpdateConsolesUsing(ent);
    }

    private static void AddDestination(DockingShuttleComponent component, LocId name, MapId map)
    {
        if (component.Destinations.Exists(destination => destination.Map == map))
            return;

        component.Destinations.Add(new DockingDestination { Name = name, Map = map });
    }

    private void RefreshConsoles()
    {
        var query = EntityQueryEnumerator<DockingConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
            _console.UpdateShuttle((uid, comp));
    }

    private void OnFTLStarted(Entity<DockingShuttleComponent> ent, ref FTLStartedEvent args)
    {
        _console.OnShuttleFTLStarted(ent);
    }

    private void OnFTLCompleted(Entity<DockingShuttleComponent> ent, ref FTLCompletedEvent args)
    {
        _console.UpdateConsolesUsing(ent);
    }

    private void OnShutdown(Entity<DockingShuttleComponent> ent, ref ComponentShutdown args)
    {
        _console.OnShuttleDeleted(ent);
    }

    private void OnStationGridAdded(StationGridAddedEvent args)
    {
        if (!TryComp<DockingShuttleComponent>(args.GridId, out var component) || component.Station != null)
            return;

        if (_station.GetOwningStation(args.GridId) is not { } station)
            return;

        component.Station = station;
        component.StationMap = Transform(args.GridId).MapID;
        AddDestination(component, Name(station), component.StationMap.Value);
        _console.UpdateConsolesUsing(args.GridId);
    }

    private void OnAddStation(Entity<DockingShuttleComponent> ent, ref ShuttleAddStationEvent args)
    {
        ent.Comp.Station = args.Station;
        ent.Comp.StationMap = args.MapId;
        AddDestination(ent.Comp, Name(args.Station), args.MapId);
        _console.UpdateConsolesUsing(ent);
    }
}

public sealed class ShuttleAddStationEvent(EntityUid station, MapId mapId) : EntityEventArgs
{
    public EntityUid Station = station;
    public MapId MapId = mapId;
}
