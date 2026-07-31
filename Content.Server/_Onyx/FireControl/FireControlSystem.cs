using System.Linq;
using System.Numerics;
using Content.Server.Power.EntitySystems;
using Content.Server.Shuttles.Systems;
using Content.Shared._Onyx.FireControl;
using Content.Shared._Onyx.ShipGuns;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Content.Shared.Power;
using Content.Shared.Shuttles.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameObjects;
using Robust.Server.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.FireControl;

public sealed partial class FireControlSystem : EntitySystem
{
    private static readonly TimeSpan PvsRefreshInterval = TimeSpan.FromSeconds(1);

    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private RotateToFaceSystem _rotate = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private ShuttleConsoleSystem _shuttleConsole = default!;
    [Dependency] private PvsOverrideSystem _pvs = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<(ICommonSession Session, EntityUid Grid), int> _pvsReferences = new();
    private readonly Dictionary<EntityUid, (EntityCoordinates Target, TimeSpan Updated)> _consoleTargets = new();
    private TimeSpan _nextPvsRefresh;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FireControlServerComponent, MapInitEvent>(OnServerMapInit);
        SubscribeLocalEvent<FireControlServerComponent, PowerChangedEvent>(OnServerPowerChanged);
        SubscribeLocalEvent<FireControlServerComponent, EntParentChangedMessage>(OnServerParentChanged);
        SubscribeLocalEvent<FireControlServerComponent, ComponentShutdown>(OnServerShutdown);
        SubscribeLocalEvent<FireControlServerComponent, ExaminedEvent>(OnServerExamined);

        SubscribeLocalEvent<FireControllableComponent, MapInitEvent>(OnControllableMapInit);
        SubscribeLocalEvent<FireControllableComponent, PowerChangedEvent>(OnControllablePowerChanged);
        SubscribeLocalEvent<FireControllableComponent, EntParentChangedMessage>(OnControllableParentChanged);
        SubscribeLocalEvent<FireControllableComponent, ComponentShutdown>(OnControllableShutdown);

        SubscribeLocalEvent<FireControlConsoleComponent, MapInitEvent>(OnConsoleMapInit);
        SubscribeLocalEvent<FireControlConsoleComponent, PowerChangedEvent>(OnConsolePowerChanged);
        SubscribeLocalEvent<FireControlConsoleComponent, EntParentChangedMessage>(OnConsoleParentChanged);
        SubscribeLocalEvent<FireControlConsoleComponent, ComponentShutdown>(OnConsoleShutdown);

        Subs.BuiEvents<FireControlConsoleComponent>(FireControlConsoleUiKey.Key, subs =>
        {
            subs.Event<FireControlConsoleRefreshServerMessage>(OnRefreshServer);
            subs.Event<FireControlConsoleFireMessage>(OnFire);
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<BoundUIClosedEvent>(OnUiClosed);
        });

        SubscribeLocalEvent<GridSplitEvent>(OnGridSplit);
        InitializeTargetGuided();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateTargetGuided();
        if (_timing.CurTime < _nextPvsRefresh)
            return;

        _nextPvsRefresh = _timing.CurTime + PvsRefreshInterval;
        var consoles = EntityQueryEnumerator<FireControlPvsComponent>();
        while (consoles.MoveNext(out var console, out var tracker))
        {
            foreach (var actor in tracker.Overrides.Keys.ToArray())
                RefreshPvs(console, actor);
        }
    }

    private void OnServerMapInit(Entity<FireControlServerComponent> ent, ref MapInitEvent args)
    {
        if (_power.IsPowered(ent.Owner))
            TryConnect(ent);
    }

    private void OnServerPowerChanged(Entity<FireControlServerComponent> ent, ref PowerChangedEvent args)
    {
        if (args.Powered)
            TryConnect(ent);
        else
            Disconnect(ent);
    }

    private void OnServerParentChanged(Entity<FireControlServerComponent> ent, ref EntParentChangedMessage args)
    {
        Disconnect(ent);
        if (_power.IsPowered(ent.Owner))
            TryConnect(ent);
    }

    private void OnServerShutdown(Entity<FireControlServerComponent> ent, ref ComponentShutdown args)
    {
        Disconnect(ent);
    }

    private void OnServerExamined(Entity<FireControlServerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("gunnery-server-examine-detail",
            ("usedProcessingPower", ent.Comp.UsedProcessingPower),
            ("processingPower", ent.Comp.ProcessingPower),
            ("valueColor", ent.Comp.UsedProcessingPower <= ent.Comp.ProcessingPower - 2 ? "green" : "yellow")));
    }

    private void OnControllableMapInit(Entity<FireControllableComponent> ent, ref MapInitEvent args)
    {
        if (_power.IsPowered(ent.Owner))
            TryRegister(ent);
    }

    private void OnControllablePowerChanged(Entity<FireControllableComponent> ent, ref PowerChangedEvent args)
    {
        if (args.Powered)
            TryRegister(ent);
        else
            Unregister(ent);
    }

    private void OnControllableParentChanged(Entity<FireControllableComponent> ent, ref EntParentChangedMessage args)
    {
        Unregister(ent);
        if (_power.IsPowered(ent.Owner))
            TryRegister(ent);
    }

    private void OnControllableShutdown(Entity<FireControllableComponent> ent, ref ComponentShutdown args)
    {
        Unregister(ent);
    }

    private void OnConsoleMapInit(Entity<FireControlConsoleComponent> ent, ref MapInitEvent args)
    {
        if (_power.IsPowered(ent.Owner))
            TryRegisterConsole(ent);
    }

    private void OnConsolePowerChanged(Entity<FireControlConsoleComponent> ent, ref PowerChangedEvent args)
    {
        if (args.Powered)
            TryRegisterConsole(ent);
        else
            UnregisterConsole(ent);
    }

    private void OnConsoleParentChanged(Entity<FireControlConsoleComponent> ent, ref EntParentChangedMessage args)
    {
        UnregisterConsole(ent);
        if (_power.IsPowered(ent.Owner))
            TryRegisterConsole(ent);
    }

    private void OnConsoleShutdown(Entity<FireControlConsoleComponent> ent, ref ComponentShutdown args)
    {
        _consoleTargets.Remove(ent.Owner);
        ClearPvs(ent.Owner);
        UnregisterConsole(ent);
    }

    private bool TryConnect(Entity<FireControlServerComponent> server)
    {
        var grid = Transform(server).GridUid;
        if (grid == null)
            return false;

        var gridComp = EnsureComp<FireControlGridComponent>(grid.Value);
        if (gridComp.ControllingServer is { } other && other != server.Owner && Exists(other))
            return false;

        gridComp.ControllingServer = server.Owner;
        server.Comp.ConnectedGrid = grid;
        RefreshControllables(grid.Value, gridComp);

        var consoles = EntityQueryEnumerator<FireControlConsoleComponent, TransformComponent>();
        while (consoles.MoveNext(out var uid, out var console, out var xform))
        {
            if (xform.GridUid == grid && _power.IsPowered(uid))
                TryRegisterConsole((uid, console));
        }

        return true;
    }

    private void Disconnect(Entity<FireControlServerComponent> server)
    {
        if (server.Comp.ConnectedGrid is { } grid && TryComp<FireControlGridComponent>(grid, out var gridComp) && gridComp.ControllingServer == server.Owner)
            RemComp<FireControlGridComponent>(grid);

        foreach (var uid in server.Comp.Controlled.ToArray())
            Unregister(uid);
        foreach (var uid in server.Comp.Consoles.ToArray())
            UnregisterConsole(uid);

        server.Comp.ConnectedGrid = null;
        server.Comp.UsedProcessingPower = 0;
    }

    private void RefreshControllables(EntityUid grid, FireControlGridComponent gridComp)
    {
        if (gridComp.ControllingServer is not { } serverUid || !TryComp<FireControlServerComponent>(serverUid, out var server))
            return;

        foreach (var uid in server.Controlled.ToArray())
            Unregister(uid);

        var query = EntityQueryEnumerator<FireControllableComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var controllable, out var xform))
        {
            if (xform.GridUid == grid && _power.IsPowered(uid))
                TryRegister((uid, controllable));
        }

        UpdateServerUis(server);
    }

    private bool TryRegister(Entity<FireControllableComponent> controllable)
    {
        if (controllable.Comp.ControllingServer != null || TryGetGridServer(controllable.Owner) is not { } server)
            return false;

        var cost = GetProcessingPowerCost(controllable.Owner);
        if (server.Comp.UsedProcessingPower + cost > server.Comp.ProcessingPower || !server.Comp.Controlled.Add(controllable.Owner))
            return false;

        server.Comp.UsedProcessingPower += cost;
        controllable.Comp.ControllingServer = server.Owner;
        UpdateServerUis(server.Comp);
        return true;
    }

    private void Unregister(EntityUid uid, FireControllableComponent? controllable = null)
    {
        if (!Resolve(uid, ref controllable, false) || controllable.ControllingServer is not { } serverUid)
            return;

        controllable.ControllingServer = null;
        if (!TryComp<FireControlServerComponent>(serverUid, out var server) || !server.Controlled.Remove(uid))
            return;

        server.UsedProcessingPower = Math.Max(0, server.UsedProcessingPower - GetProcessingPowerCost(uid));
        UpdateServerUis(server);
    }

    private int GetProcessingPowerCost(EntityUid uid)
    {
        if (!TryComp<ShipGunClassComponent>(uid, out var gunClass))
            return 0;

        return gunClass.Class switch
        {
            ShipGunClass.Light => 1,
            ShipGunClass.Medium => 2,
            ShipGunClass.Heavy => 4,
            _ => 0,
        };
    }

    private Entity<FireControlServerComponent>? TryGetGridServer(EntityUid uid)
    {
        var grid = Transform(uid).GridUid;
        if (grid == null ||
            !TryComp<FireControlGridComponent>(grid, out var gridComp) ||
            gridComp.ControllingServer is not { } serverUid ||
            !TryComp<FireControlServerComponent>(serverUid, out var server))
            return null;

        return (serverUid, server);
    }

    private bool TryRegisterConsole(Entity<FireControlConsoleComponent> console)
    {
        if (console.Comp.ConnectedServer != null || TryGetGridServer(console.Owner) is not { } server)
            return false;

        console.Comp.ConnectedServer = server.Owner;
        server.Comp.Consoles.Add(console.Owner);
        UpdateUi(console);
        return true;
    }

    private void UnregisterConsole(EntityUid uid, FireControlConsoleComponent? console = null)
    {
        if (!Resolve(uid, ref console, false))
            return;

        ClearPvs(uid);
        if (console.ConnectedServer is { } serverUid && TryComp<FireControlServerComponent>(serverUid, out var server))
            server.Consoles.Remove(uid);
        console.ConnectedServer = null;
        UpdateUi(uid, console);
    }

    private void OnRefreshServer(Entity<FireControlConsoleComponent> console, ref FireControlConsoleRefreshServerMessage args)
    {
        if (!_ui.IsUiOpen(console.Owner, FireControlConsoleUiKey.Key, args.Actor))
            return;

        if (console.Comp.ConnectedServer == null)
            TryRegisterConsole(console);
        if (console.Comp.ConnectedServer is { } serverUid && TryComp<FireControlServerComponent>(serverUid, out var server) && server.ConnectedGrid is { } grid && TryComp<FireControlGridComponent>(grid, out var gridComp))
            RefreshControllables(grid, gridComp);
        RefreshPvs(console.Owner, args.Actor);
    }

    private void OnFire(Entity<FireControlConsoleComponent> console, ref FireControlConsoleFireMessage args)
    {
        if (!_ui.IsUiOpen(console.Owner, FireControlConsoleUiKey.Key, args.Actor) ||
            console.Comp.ConnectedServer is not { } serverUid ||
            !TryComp<FireControlServerComponent>(serverUid, out var server) ||
            args.Selected.Count == 0 ||
            args.Selected.Count > server.Controlled.Count)
            return;

        _consoleTargets[console.Owner] = (GetCoordinates(args.Coordinates), _timing.CurTime);
        FireWeapons(console.Owner, (serverUid, server), args.Selected, args.Coordinates);
    }

    private void FireWeapons(EntityUid console, Entity<FireControlServerComponent> server, List<NetEntity> weapons, NetCoordinates netCoordinates)
    {
        if (server.Comp.ConnectedGrid is not { } grid || HasComp<FTLComponent>(grid))
            return;

        var target = GetCoordinates(netCoordinates);
        var targetMap = _transform.ToMapCoordinates(target);
        var seen = new HashSet<EntityUid>();

        foreach (var netWeapon in weapons)
        {
            if (!TryGetEntity(netWeapon, out var weapon) || !seen.Add(weapon.Value) ||
                !server.Comp.Controlled.Contains(weapon.Value) ||
                !TryComp<FireControllableComponent>(weapon, out var controllable) || controllable.ControllingServer != server.Owner ||
                !TryComp<GunComponent>(weapon, out var gun) || !_power.IsPowered(weapon.Value))
                continue;

            var xform = Transform(weapon.Value);
            if (xform.MapID != targetMap.MapId)
                continue;

            var origin = _transform.GetWorldPosition(xform);
            var delta = targetMap.Position - origin;
            if (delta.LengthSquared() <= 0.01f || !HasLineOfSight(weapon.Value, origin, targetMap.Position, xform.MapID))
                continue;

            _rotate.TryRotateTo(weapon.Value, Angle.FromWorldVec(delta), 0f, Angle.FromDegrees(1), float.MaxValue, xform);
            FireGuided(console, weapon.Value, (weapon.Value, gun), target);
        }
    }

    private bool HasLineOfSight(EntityUid weapon, Vector2 origin, Vector2 target, MapId mapId)
    {
        var delta = target - origin;
        var distance = delta.Length();
        if (distance <= 0f)
            return false;

        var weaponGrid = Transform(weapon).GridUid;
        var ray = new CollisionRay(origin, delta / distance, (int) (CollisionGroup.Opaque | CollisionGroup.Impassable));
        return !_physics.IntersectRayWithPredicate(mapId, ray, weapon,
            (entity, source) => entity == source || weaponGrid != null && Transform(entity).GridUid != weaponGrid,
            Math.Min(distance, 500f), true).Any();
    }

    private void OnUiOpened(Entity<FireControlConsoleComponent> console, ref BoundUIOpenedEvent args)
    {
        UpdateUi(console);
        RefreshPvs(console.Owner, args.Actor);
    }

    private void OnUiClosed(Entity<FireControlConsoleComponent> console, ref BoundUIClosedEvent args)
    {
        ClearPvs(console.Owner, args.Actor);
    }

    private void RefreshPvs(EntityUid console, EntityUid actor)
    {
        if (!TryComp<ActorComponent>(actor, out var actorComp) || !TryComp<RadarConsoleComponent>(console, out var radar))
            return;

        var tracker = EnsureComp<FireControlPvsComponent>(console);
        var overrides = tracker.Overrides.GetOrNew(actor);
        var desired = new HashSet<EntityUid>();
        var xform = Transform(console);
        var origin = _transform.GetWorldPosition(xform);
        var rangeSquared = radar.MaxRange * radar.MaxRange;
        var grids = EntityQueryEnumerator<MapGridComponent, TransformComponent>();

        while (grids.MoveNext(out var grid, out var mapGrid, out var gridXform))
        {
            if (gridXform.MapID != xform.MapID)
                continue;

            var bounds = _transform.GetWorldMatrix(gridXform).TransformBox(mapGrid.LocalAABB);
            var nearest = Vector2.Clamp(origin, bounds.BottomLeft, bounds.TopRight);
            if (Vector2.DistanceSquared(origin, nearest) > rangeSquared)
                continue;

            desired.Add(grid);
        }

        foreach (var grid in overrides.Except(desired).ToArray())
        {
            RemovePvsReference(actorComp.PlayerSession, grid);
            overrides.Remove(grid);
        }

        foreach (var grid in desired.Except(overrides))
        {
            AddPvsReference(actorComp.PlayerSession, grid);
            overrides.Add(grid);
        }
    }

    private void ClearPvs(EntityUid console, EntityUid? actor = null)
    {
        if (!TryComp<FireControlPvsComponent>(console, out var tracker))
            return;

        foreach (var (viewer, grids) in tracker.Overrides.ToArray())
        {
            if (actor != null && viewer != actor)
                continue;

            if (!TryComp<ActorComponent>(viewer, out var actorComp))
            {
                tracker.Overrides.Remove(viewer);
                continue;
            }

            foreach (var grid in grids)
                RemovePvsReference(actorComp.PlayerSession, grid);
            tracker.Overrides.Remove(viewer);
        }
    }

    private void AddPvsReference(ICommonSession session, EntityUid grid)
    {
        var key = (session, grid);
        if (_pvsReferences.TryGetValue(key, out var count))
        {
            _pvsReferences[key] = count + 1;
            return;
        }

        _pvsReferences[key] = 1;
        _pvs.AddSessionOverride(grid, session);
    }

    private void RemovePvsReference(ICommonSession session, EntityUid grid)
    {
        var key = (session, grid);
        if (!_pvsReferences.TryGetValue(key, out var count))
            return;

        if (count > 1)
        {
            _pvsReferences[key] = count - 1;
            return;
        }

        _pvsReferences.Remove(key);
        _pvs.RemoveSessionOverride(grid, session);
    }

    private void UpdateServerUis(FireControlServerComponent server)
    {
        foreach (var console in server.Consoles)
            UpdateUi(console);
    }

    private void UpdateUi(EntityUid uid, FireControlConsoleComponent? console = null)
    {
        if (!Resolve(uid, ref console, false))
            return;

        var entries = new List<FireControllableEntry>();
        if (console.ConnectedServer is { } serverUid && TryComp<FireControlServerComponent>(serverUid, out var server))
        {
            foreach (var controllable in server.Controlled)
                entries.Add(new FireControllableEntry(GetNetEntity(controllable), GetNetCoordinates(Transform(controllable).Coordinates), Name(controllable)));
        }

        var nav = _shuttleConsole.GetNavState(uid, _shuttleConsole.GetAllDocks());
        _ui.SetUiState(uid, FireControlConsoleUiKey.Key,
            new FireControlConsoleBoundInterfaceState(console.ConnectedServer != null, entries.ToArray(), nav));
    }

    private void OnGridSplit(ref GridSplitEvent args)
    {
        var query = EntityQueryEnumerator<FireControlServerComponent>();
        while (query.MoveNext(out var uid, out var server))
        {
            if (server.ConnectedGrid is { } grid && (grid == args.Grid || args.NewGrids.Contains(grid)) && TryComp<FireControlGridComponent>(grid, out var gridComp))
                RefreshControllables(grid, gridComp);
        }
    }
}
