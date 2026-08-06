using System.Linq;
using Content.Shared._Onyx.ZLevels.Core.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Onyx.ZLevels.Core;

/// <summary>
/// Reconstructs physical grid networks from anchored Z-grid connectors.
/// </summary>
public sealed partial class CEZGridConnectorSystem : EntitySystem
{
    [Dependency] private CEZLevelsSystem _zLevels = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private bool _dirty;
    private bool _hasConnectors;
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _adjacency = new();
    private readonly List<HashSet<EntityUid>> _components = new();
    private readonly HashSet<EntityUid> _visited = new();
    private readonly Queue<EntityUid> _queue = new();
    private readonly Dictionary<EntityUid, EntityUid> _targets = new();
    private readonly HashSet<EntityUid> _claimedNetworks = new();
    private readonly HashSet<EntityUid> _connectorGrids = new();
    private readonly List<EntityUid> _remove = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEZGridConnectorComponent, MapInitEvent>(OnConnectorMapInit);
        SubscribeLocalEvent<CEZGridConnectorComponent, AnchorStateChangedEvent>(OnConnectorAnchorChanged);
        SubscribeLocalEvent<CEZGridConnectorComponent, EntityTerminatingEvent>(OnConnectorTerminating);
        SubscribeLocalEvent<CEZGridComponent, EntityTerminatingEvent>(OnGridTerminating);
        SubscribeLocalEvent<CEZGridNetworkComponent, ComponentShutdown>(OnNetworkShutdown);
        SubscribeLocalEvent<MapGridComponent, MapInitEvent>(OnGridMapInit);
        SubscribeLocalEvent<MapUidChangedEvent>(OnMapUidChanged);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<GridSplitEvent>(OnGridSplit);
    }

    public void MarkDirty()
    {
        _dirty = true;
    }

    private void OnConnectorMapInit(Entity<CEZGridConnectorComponent> ent, ref MapInitEvent args)
    {
        _hasConnectors = true;
        _dirty = true;
    }

    private void OnConnectorAnchorChanged(Entity<CEZGridConnectorComponent> ent, ref AnchorStateChangedEvent args)
    {
        _dirty = true;
    }

    private void OnConnectorTerminating(Entity<CEZGridConnectorComponent> ent, ref EntityTerminatingEvent args)
    {
        _dirty = true;
    }

    private void OnGridTerminating(Entity<CEZGridComponent> ent, ref EntityTerminatingEvent args)
    {
        _dirty = true;
    }

    private void OnTileChanged(ref TileChangedEvent args)
    {
        if (_hasConnectors)
            _dirty = true;
    }

    private void OnGridSplit(ref GridSplitEvent args)
    {
        _dirty = true;
    }

    private void OnNetworkShutdown(Entity<CEZGridNetworkComponent> ent, ref ComponentShutdown args)
    {
        _zLevels.ClearConnectorLinkage(ent.Owner, ent.Comp.Grids);
        foreach (var grid in ent.Comp.Grids.ToList())
        {
            if (TryComp<CEZGridComponent>(grid, out var component) && component.Network == ent.Owner)
                _zLevels.TryRemoveGridFromNetwork(grid);
        }
    }

    private void OnGridMapInit(Entity<MapGridComponent> ent, ref MapInitEvent args)
    {
        _dirty = true;
    }

    private void OnMapUidChanged(ref MapUidChangedEvent args)
    {
        if (HasComp<MapGridComponent>(args.Uid))
            _dirty = true;
    }

    public override void Update(float frameTime)
    {
        if (!_dirty)
            return;

        _dirty = false;
        Recalculate();
    }

    private void Recalculate()
    {
        ComputeComponents();
        _claimedNetworks.Clear();
        _targets.Clear();

        foreach (var component in _components)
        {
            var target = PickSurvivor(component);
            if (!target.IsValid())
                target = _zLevels.CreateGridNetwork().Owner;

            _claimedNetworks.Add(target);
            foreach (var grid in component)
                _targets[grid] = target;
        }

        _remove.Clear();
        var networks = EntityQueryEnumerator<CEZGridNetworkComponent>();
        while (networks.MoveNext(out var networkUid, out var network))
        {
            foreach (var grid in network.Grids)
            {
                if (!_targets.TryGetValue(grid, out var target) || target != networkUid)
                    _remove.Add(grid);
            }
        }

        foreach (var grid in _remove)
            _zLevels.TryRemoveGridFromNetwork(grid);

        foreach (var (grid, target) in _targets)
        {
            if (TryComp<CEZGridNetworkComponent>(target, out var network) && !network.Grids.Contains(grid))
                _zLevels.TryAddGridToNetwork((target, network), grid);
        }

        foreach (var networkUid in _claimedNetworks)
        {
            if (TryComp<CEZGridNetworkComponent>(networkUid, out var network))
                _zLevels.RebuildGridLinkage((networkUid, network));
        }

        _components.Clear();
    }

    private EntityUid PickSurvivor(HashSet<EntityUid> component)
    {
        var best = EntityUid.Invalid;
        var bestOverlap = 0;
        var networks = EntityQueryEnumerator<CEZGridNetworkComponent>();
        while (networks.MoveNext(out var uid, out var network))
        {
            if (_claimedNetworks.Contains(uid))
                continue;

            var overlap = network.Grids.Count(component.Contains);
            if (overlap <= bestOverlap)
                continue;

            best = uid;
            bestOverlap = overlap;
        }

        return best;
    }

    private void ComputeComponents()
    {
        _adjacency.Clear();
        _connectorGrids.Clear();
        AddConnectorEdges();
        AddFallbackEdges();

        _visited.Clear();
        foreach (var start in _adjacency.Keys)
        {
            if (!_visited.Add(start))
                continue;

            var component = new HashSet<EntityUid>();
            _queue.Clear();
            _queue.Enqueue(start);
            while (_queue.TryDequeue(out var grid))
            {
                component.Add(grid);
                foreach (var peer in _adjacency[grid])
                {
                    if (_visited.Add(peer))
                        _queue.Enqueue(peer);
                }
            }

            _components.Add(component);
        }
    }

    private void AddConnectorEdges()
    {
        _hasConnectors = false;
        var connectors = EntityQueryEnumerator<CEZGridConnectorComponent, TransformComponent>();
        while (connectors.MoveNext(out var connector, out _, out var xform))
        {
            _hasConnectors = true;
            if (TerminatingOrDeleted(connector) || !xform.Anchored || xform.GridUid is not { } lowerGrid || xform.MapUid is not { } mapUid)
                continue;

            if (!TryComp<CEZLevelMapComponent>(mapUid, out var zMap) ||
                !_zLevels.TryMapUp((mapUid, zMap), out var aboveMap) ||
                !TryComp<MapComponent>(aboveMap.Value.Owner, out var aboveMapComp))
            {
                continue;
            }

            var worldPosition = _transform.GetWorldPosition(connector);
            if (!_map.TryFindGridAt(aboveMapComp.MapId, worldPosition, out var upperGrid, out var upperGridComp) ||
                upperGrid == lowerGrid ||
                !_map.TryGetTileRef(upperGrid, upperGridComp, worldPosition, out var tile) ||
                tile.Tile.IsEmpty)
            {
                continue;
            }

            AddEdge(lowerGrid, upperGrid);
            _connectorGrids.Add(lowerGrid);
            _connectorGrids.Add(upperGrid);
        }
    }

    private void AddFallbackEdges()
    {
        var selected = new Dictionary<EntityUid, Dictionary<int, EntityUid>>();
        var grids = AllEntityQuery<MapGridComponent, TransformComponent>();
        while (grids.MoveNext(out var grid, out _, out var xform))
        {
            if (_connectorGrids.Contains(grid) || xform.MapUid is not { } mapUid ||
                !TryComp<CEZLevelMapComponent>(mapUid, out var zMap))
            {
                continue;
            }

            if (!selected.TryGetValue(zMap.NetworkUid, out var byDepth))
                selected[zMap.NetworkUid] = byDepth = new Dictionary<int, EntityUid>();

            if (!byDepth.TryGetValue(zMap.Depth, out var existing) || grid.Id < existing.Id)
                byDepth[zMap.Depth] = grid;
        }

        foreach (var byDepth in selected.Values)
        {
            if (byDepth.Count < 2)
                continue;

            var ordered = byDepth.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToArray();
            for (var i = 1; i < ordered.Length; i++)
                AddEdge(ordered[i - 1], ordered[i]);
        }
    }

    private void AddEdge(EntityUid first, EntityUid second)
    {
        if (!_adjacency.TryGetValue(first, out var firstPeers))
            _adjacency[first] = firstPeers = new HashSet<EntityUid>();
        if (!_adjacency.TryGetValue(second, out var secondPeers))
            _adjacency[second] = secondPeers = new HashSet<EntityUid>();
        firstPeers.Add(second);
        secondPeers.Add(first);
    }
}
