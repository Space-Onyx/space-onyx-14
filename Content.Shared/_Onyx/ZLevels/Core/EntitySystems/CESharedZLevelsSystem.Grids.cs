using Content.Shared._Onyx.ZLevels.Core.Components;
using JetBrains.Annotations;

namespace Content.Shared._Onyx.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem
{
    private EntityQuery<CEZGridComponent> _zGridQuery;
    private EntityQuery<CEZGridNetworkComponent> _zGridNetworkQuery;

    private void InitGridNetworks()
    {
        _zGridQuery = GetEntityQuery<CEZGridComponent>();
        _zGridNetworkQuery = GetEntityQuery<CEZGridNetworkComponent>();
    }

    [PublicAPI]
    public int? TryGetGridZDepth(EntityUid gridUid)
    {
        var mapUid = Transform(gridUid).MapUid;
        return mapUid.HasValue && _zMapQuery.TryComp(mapUid.Value, out var zMap) ? zMap.Depth : null;
    }

    [PublicAPI]
    public bool TryGetGridNetwork(EntityUid grid, out Entity<CEZGridNetworkComponent> network)
    {
        network = default;
        if (!_zGridQuery.TryComp(grid, out var gridComp) || string.IsNullOrEmpty(gridComp.NetworkId))
            return false;

        if (gridComp.Network.IsValid() && _zGridNetworkQuery.TryComp(gridComp.Network, out var cached))
        {
            network = (gridComp.Network, cached);
            return true;
        }

        var query = EntityQueryEnumerator<CEZGridNetworkComponent>();
        while (query.MoveNext(out var uid, out var candidate))
        {
            if (candidate.NetworkId != gridComp.NetworkId)
                continue;

            gridComp.Network = uid;
            network = (uid, candidate);
            return true;
        }

        return false;
    }
}

public sealed class CEZLevelGridNetworkUpdatedEvent : EntityEventArgs;

[ByRefEvent]
public readonly struct CEGridAddedIntoZNetworkEvent(Entity<CEZGridNetworkComponent> network)
{
    public readonly Entity<CEZGridNetworkComponent> Network = network;
}

[ByRefEvent]
public readonly struct CEGridRemovedFromZNetworkEvent(Entity<CEZGridNetworkComponent> network)
{
    public readonly Entity<CEZGridNetworkComponent> Network = network;
}
