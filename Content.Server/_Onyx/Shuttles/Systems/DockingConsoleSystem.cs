using Content.Server._Onyx.Salvage.Procedural.Components;
using Content.Server._Onyx.Salvage.DeathRattle;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Onyx.Shuttles;
using Content.Shared._Onyx.Shuttles.Components;
using Content.Shared._Onyx.Shuttles.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Access.Systems;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Station.Components;
using Content.Shared.Timing;
using Content.Shared.Whitelist;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server._Onyx.Shuttles.Systems;

public sealed partial class DockingConsoleSystem : SharedDockingConsoleSystem
{
    private static readonly ResPath MiningShuttlePath = new("/Maps/_Onyx/Shuttles/mining.yml");

    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private MapSystem _mapSystem = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private AccessReaderSystem _access = default!;

    private bool _callInProgress;
    private readonly Dictionary<EntityUid, MapId> _stagingMaps = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DockingConsoleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DockEvent>(OnDock);
        SubscribeLocalEvent<UndockEvent>(OnUndock);
        SubscribeLocalEvent<FTLCompletedEvent>(OnFTLCompleted);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        Subs.BuiEvents<DockingConsoleComponent>(DockingConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<DockingConsoleFTLMessage>(OnFTL);
            subs.Event<DockingConsoleShuttleCheckMessage>(OnCallShuttle);
        });
    }

    private void OnMapInit(Entity<DockingConsoleComponent> ent, ref MapInitEvent args)
    {
        UpdateShuttle(ent);
        UpdateUI(ent);
    }

    private void OnDock(DockEvent args)
    {
        UpdateConsolesUsing(args.GridAUid);
        UpdateConsolesUsing(args.GridBUid);
    }

    private void OnUndock(UndockEvent args)
    {
        UpdateConsolesUsing(args.GridAUid);
        UpdateConsolesUsing(args.GridBUid);
    }

    private void OnFTLCompleted(ref FTLCompletedEvent args)
    {
        UpdateConsolesUsing(args.Entity);

        if (!TryComp<FTLComponent>(args.Entity, out var ftl))
            return;

        var shuttle = args.Entity;
        Timer.Spawn(ftl.StateTime.Length + TimeSpan.FromSeconds(1), () =>
        {
            if (Exists(shuttle))
                UpdateConsolesUsing(shuttle);
        });
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        CleanupStagingMaps();
    }

    internal void CleanupStagingMaps()
    {
        var maps = new List<MapId>(_stagingMaps.Values);
        _stagingMaps.Clear();
        _callInProgress = false;

        foreach (var map in maps)
            _mapSystem.DeleteMap(map);
    }

    private void OnOpened(Entity<DockingConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (TerminatingOrDeleted(ent.Comp.Shuttle))
            UpdateShuttle(ent);

        UpdateUI(ent);
    }

    public void OnShuttleFTLStarted(EntityUid shuttle)
    {
        UpdateConsolesUsing(shuttle);
        if (_stagingMaps.Remove(shuttle, out var map))
            _mapSystem.DeleteMap(map);
    }

    public void OnShuttleDeleted(EntityUid shuttle)
    {
        if (_stagingMaps.Remove(shuttle, out var map))
            _mapSystem.DeleteMap(map);

        var query = EntityQueryEnumerator<DockingConsoleComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.Shuttle != shuttle)
                continue;

            component.Shuttle = null;
            component.HasShuttle = false;
            Dirty(uid, component);
            UpdateUI((uid, component));
        }
    }

    public void UpdateConsolesUsing(EntityUid shuttle)
    {
        if (!HasComp<DockingShuttleComponent>(shuttle))
            return;

        var query = EntityQueryEnumerator<DockingConsoleComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.Shuttle == shuttle)
                UpdateUI((uid, component));
        }
    }

    public void UpdateUI(Entity<DockingConsoleComponent> ent)
    {
        if (TerminatingOrDeleted(ent.Comp.Shuttle))
        {
            ent.Comp.Shuttle = null;
            ent.Comp.HasShuttle = false;
            Dirty(ent);
            _ui.SetUiState(ent.Owner,
                DockingConsoleUiKey.Key,
                new DockingConsoleState(false, FTLState.Invalid, default, new List<DockingDestination>()));
            return;
        }

        var shuttle = ent.Comp.Shuttle!.Value;
        var state = FTLState.Available;
        StartEndTime time = default;
        var destinations = new List<DockingDestination>();

        if (TryComp<FTLComponent>(shuttle, out var ftl))
        {
            state = ftl.State;
            time = _shuttle.GetStateTime(ftl);
        }

        if (TryComp<DockingShuttleComponent>(shuttle, out var docking))
            destinations = docking.Destinations;

        _ui.SetUiState(ent.Owner, DockingConsoleUiKey.Key, new DockingConsoleState(true, state, time, destinations));
    }

    private void OnFTL(Entity<DockingConsoleComponent> ent, ref DockingConsoleFTLMessage args)
    {
        if (ent.Comp.Shuttle is not { } shuttle ||
            !TryComp<DockingShuttleComponent>(shuttle, out var docking) ||
            !TryComp<ShuttleComponent>(shuttle, out var shuttleComponent) ||
            !_access.IsAllowed(args.Actor, ent) ||
            !_shuttle.CanFTL(shuttle, out _) ||
            !IsAllowedDestination((shuttle, docking), args.Destination) ||
            args.Destination == Transform(shuttle).MapID ||
            FindLargestGrid(args.Destination) is not { } grid)
        {
            return;
        }

        _shuttle.FTLToDock(shuttle, shuttleComponent, grid, priorityTag: docking.DockTag);
        UpdateUI(ent);
    }

    internal bool IsAllowedDestination(Entity<DockingShuttleComponent> shuttle, MapId mapId)
    {
        var destinationQuery = EntityQueryEnumerator<FTLDestinationComponent, MapComponent>();
        while (destinationQuery.MoveNext(out _, out var destination, out var map))
        {
            if (map.MapId == mapId && destination.Enabled &&
                !_whitelist.IsWhitelistFailOrNull(destination.Whitelist, shuttle))
                return true;
        }

        if (HasComp<MiningShuttleComponent>(shuttle))
        {
            var lavalandQuery = EntityQueryEnumerator<LavalandMapComponent, MapComponent>();
            while (lavalandQuery.MoveNext(out _, out _, out var map))
            {
                if (map.MapId == mapId)
                    return true;
            }
        }

        return shuttle.Comp.StationMap == mapId &&
               shuttle.Comp.Station is { } station &&
               Exists(station);
    }

    private void OnCallShuttle(Entity<DockingConsoleComponent> ent, ref DockingConsoleShuttleCheckMessage args)
    {
        if (!_access.IsAllowed(args.Actor, ent) || _callInProgress || UpdateShuttle(ent))
            return;

        var targetMap = Transform(ent).MapID;
        if (FindLargestGrid(targetMap) is not { } targetGrid || _station.GetOwningStation(targetGrid) is not { } station)
            return;

        _callInProgress = true;
        _mapSystem.CreateMap(out var stagingMap);

        try
        {
            if (!_mapLoader.TryLoadGrid(stagingMap, MiningShuttlePath, out var loaded) || loaded is not { } shuttleEntity)
            {
                Log.Error("Failed to call the mining shuttle: map load failed.");
                return;
            }

            var shuttle = shuttleEntity.Owner;

            if (!TryComp<DockingShuttleComponent>(shuttle, out var docking) ||
                !TryComp<ShuttleComponent>(shuttle, out var shuttleComponent) ||
                !_shuttle.CanFTL(shuttle, out _))
            {
                Log.Error("Failed to call the mining shuttle: loaded grid lacks required shuttle components.");
                QueueDel(shuttle);
                return;
            }

            ent.Comp.Shuttle = shuttle;
            ent.Comp.HasShuttle = true;
            Dirty(ent);

            RaiseLocalEvent(shuttle, new ShuttleAddStationEvent(station, targetMap));
            TrackStagingMap(shuttle, stagingMap);
            _shuttle.FTLToDock(shuttle, shuttleComponent, targetGrid, priorityTag: docking.DockTag);
            if (!HasComp<FTLComponent>(shuttle))
            {
                _stagingMaps.Remove(shuttle);
                ent.Comp.Shuttle = null;
                ent.Comp.HasShuttle = false;
                Dirty(ent);
                Log.Error("Failed to call the mining shuttle: FTL request was rejected.");
                return;
            }

            UpdateAllConsoles(shuttle);
            stagingMap = MapId.Nullspace;
        }
        finally
        {
            if (stagingMap != MapId.Nullspace)
                _mapSystem.DeleteMap(stagingMap);

            _callInProgress = false;
        }
    }

    internal void TrackStagingMap(EntityUid shuttle, MapId map)
    {
        _stagingMaps[shuttle] = map;
    }

    private void UpdateAllConsoles(EntityUid shuttle)
    {
        var query = EntityQueryEnumerator<DockingConsoleComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (!_whitelist.IsValid(component.ShuttleWhitelist, shuttle))
                continue;

            component.Shuttle = shuttle;
            component.HasShuttle = true;
            Dirty(uid, component);
            UpdateUI((uid, component));
        }
    }

    public bool UpdateShuttle(Entity<DockingConsoleComponent> ent)
    {
        if (ent.Comp.Shuttle is { } current && Exists(current) && _whitelist.IsValid(ent.Comp.ShuttleWhitelist, current))
            return true;

        ent.Comp.Shuttle = FindShuttle(ent.Comp.ShuttleWhitelist);
        var hasShuttle = ent.Comp.Shuttle != null;
        if (ent.Comp.HasShuttle != hasShuttle)
        {
            ent.Comp.HasShuttle = hasShuttle;
            Dirty(ent);
        }

        return hasShuttle;
    }

    private EntityUid? FindShuttle(EntityWhitelist whitelist)
    {
        var query = EntityQueryEnumerator<DockingShuttleComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!TerminatingOrDeleted(uid) && _whitelist.IsValid(whitelist, uid))
                return uid;
        }

        return null;
    }

    private EntityUid? FindLargestGrid(MapId map)
    {
        var lavalandQuery = EntityQueryEnumerator<LavalandPlanetComponent, MapComponent>();
        while (lavalandQuery.MoveNext(out _, out var lavaland, out var mapComponent))
        {
            if (mapComponent.MapId != map)
                continue;

            foreach (var layout in lavaland.LayoutGrids)
            {
                if (Exists(layout))
                    return layout;
            }
        }

        EntityUid? largest = null;
        var largestSize = 0f;
        var query = EntityQueryEnumerator<MapGridComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var grid, out var transform))
        {
            if (transform.MapID != map)
                continue;

            if (TryComp<StationMemberComponent>(uid, out var member))
                return _station.GetLargestGrid(member.Station);

            var size = grid.LocalAABB.Size.LengthSquared();
            if (size < largestSize)
                continue;

            largest = uid;
            largestSize = size;
        }

        return largest;
    }
}
