using System.Linq;
using Content.Shared._Onyx.ZLevels.Core.Components;
using Content.Shared._Onyx.ZLevels.Core.EntitySystems;
using JetBrains.Annotations;
using Robust.Shared.Map.Components;

namespace Content.Server._Onyx.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    [PublicAPI]
    public Entity<CEZGridNetworkComponent> CreateGridNetwork()
    {
        var uid = Spawn();
        var component = EnsureComp<CEZGridNetworkComponent>(uid);
        component.NetworkId = Guid.NewGuid().ToString("N");
        Dirty(uid, component);
        return (uid, component);
    }

    [PublicAPI]
    public bool TryAddGridToNetwork(Entity<CEZGridNetworkComponent> network, EntityUid grid)
    {
        if (!HasComp<MapGridComponent>(grid) || TryGetGridNetwork(grid, out _))
            return false;

        network.Comp.Grids.Add(grid);
        var gridComp = EnsureComp<CEZGridComponent>(grid);
        gridComp.NetworkId = network.Comp.NetworkId;
        gridComp.Network = network.Owner;
        Dirty(network);
        Dirty(grid, gridComp);

        var added = new CEGridAddedIntoZNetworkEvent(network);
        RaiseLocalEvent(grid, ref added);
        RaiseLocalEvent(network, new CEZLevelGridNetworkUpdatedEvent());
        return true;
    }

    [PublicAPI]
    public bool TryRemoveGridFromNetwork(EntityUid grid)
    {
        if (!TryGetGridNetwork(grid, out var network) || !TryComp<CEZGridComponent>(grid, out _))
            return false;

        network.Comp.Grids.Remove(grid);
        var removed = new CEGridRemovedFromZNetworkEvent(network);
        RaiseLocalEvent(grid, ref removed);
        if (TryComp<CEZLinkedGridComponent>(grid, out var linked) && linked.LinkNetwork == network.Owner)
            RemComp<CEZLinkedGridComponent>(grid);
        RemComp<CEZGridComponent>(grid);

        if (TerminatingOrDeleted(network.Owner))
            return true;

        Dirty(network);
        if (network.Comp.Grids.Count == 0)
            QueueDel(network);
        else
            RaiseLocalEvent(network, new CEZLevelGridNetworkUpdatedEvent());

        return true;
    }

    [PublicAPI]
    public void DeleteGridNetwork(Entity<CEZGridNetworkComponent> network)
    {
        foreach (var grid in network.Comp.Grids.ToList())
            TryRemoveGridFromNetwork(grid);

        if (!TerminatingOrDeleted(network.Owner))
            QueueDel(network);
    }

    internal void RebuildGridLinkage(Entity<CEZGridNetworkComponent> gridNetwork)
    {
        var byDepth = new Dictionary<int, EntityUid>();
        EntityUid mapNetwork = EntityUid.Invalid;

        foreach (var grid in gridNetwork.Comp.Grids)
        {
            var depth = TryGetGridZDepth(grid);
            var mapUid = Transform(grid).MapUid;
            if (depth == null || mapUid == null || !TryComp<CEZLevelMapComponent>(mapUid.Value, out var zMap))
                return;

            if (!mapNetwork.IsValid())
                mapNetwork = zMap.NetworkUid;
            else if (mapNetwork != zMap.NetworkUid)
                return;

            // Current movement/render APIs select one corresponding peer per depth. Keep the full
            // connector topology in CEZGridNetworkComponent, but activate rigid linkage only when
            // that projection is unambiguous.
            if (!byDepth.TryAdd(depth.Value, grid))
            {
                ClearConnectorLinkage(gridNetwork.Owner, gridNetwork.Comp.Grids);
                return;
            }
        }

        if (byDepth.Count >= 2 && mapNetwork.IsValid())
        {
            if (LinkageMatches(gridNetwork.Owner, mapNetwork, byDepth))
                return;

            ClearConnectorLinkage(gridNetwork.Owner, gridNetwork.Comp.Grids);
            ApplyLinkage(mapNetwork, gridNetwork.Owner, byDepth);
            RaiseLocalEvent(gridNetwork, new CEZLevelGridNetworkUpdatedEvent());
        }
        else
        {
            ClearConnectorLinkage(gridNetwork.Owner, gridNetwork.Comp.Grids);
        }
    }

    private bool LinkageMatches(EntityUid linkNetwork, EntityUid mapNetwork, Dictionary<int, EntityUid> byDepth)
    {
        foreach (var (depth, grid) in byDepth)
        {
            if (!TryComp<CEZLinkedGridComponent>(grid, out var linked) ||
                linked.LinkNetwork != linkNetwork ||
                linked.ZNetwork != mapNetwork ||
                linked.Depth != depth ||
                linked.PeerGrids.Count < byDepth.Count - 1)
            {
                return false;
            }

            foreach (var (peerDepth, peer) in byDepth)
            {
                if (peerDepth == depth)
                    continue;
                if (!linked.PeerGrids.TryGetValue(peerDepth, out var existing) || existing != peer)
                    return false;
            }
        }

        return true;
    }

    internal void ClearConnectorLinkage(EntityUid gridNetwork, IEnumerable<EntityUid> grids)
    {
        foreach (var grid in grids)
        {
            if (TryComp<CEZLinkedGridComponent>(grid, out var linked) && linked.LinkNetwork == gridNetwork)
                RemComp<CEZLinkedGridComponent>(grid);
        }
    }

}
