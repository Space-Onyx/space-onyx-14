/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server._Onyx.PVS;
using Content.Shared._Onyx.ZLevels.Core.Components;
using JetBrains.Annotations;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.ZLevels.Core;

public sealed partial class CEZLevelsSystem
{
    /// <summary>
    /// Creates a new entity zLevelNetwork
    /// </summary>
    [PublicAPI]
    public Entity<CEZLevelsNetworkComponent> CreateZNetwork(ComponentRegistry? components = null)
    {
        var ent = Spawn();

        var zLevel = EnsureComp<CEZLevelsNetworkComponent>(ent);
        EnsureComp<CEPvsOverrideComponent>(ent);

        zLevel.Components = components ?? new ComponentRegistry();

        return (ent, zLevel);
    }

    /// <summary>
    /// Attempts to add the specified map to the zNetwork network at the specified depth.
    /// </summary>
    private bool TryAddMapIntoZNetwork(
        Entity<CEZLevelsNetworkComponent> network,
        EntityUid mapUid,
        int depth,
        out MapComponentSnapshot snapshot)
    {
        snapshot = default;

        if (!HasComp<MapComponent>(mapUid))
        {
            Log.Error($"Failed to add {ToPrettyString(mapUid)} to ZLevelNetwork {network}: not a map entity.");
            return false;
        }

        if (network.Comp.ZLevels.ContainsKey(depth))
        {
            Log.Error($"Failed to add map {mapUid} to ZLevelNetwork {network}: This depth is already occupied.");
            return false;
        }

        if (TryGetZNetwork(mapUid, out var otherNetwork))
        {
            Log.Error($"Failed attempt to add map {mapUid} to ZLevelNetwork {network}: This map is already in another network {otherNetwork}.");
            return false;
        }

        if (network.Comp.ZLevels.ContainsValue(mapUid))
        {
            Log.Error($"Failed attempt to add map {mapUid} to ZLevelNetwork {network} at depth {depth}: This map is already in this network.");
            return false;
        }

        var existed = EnsureComp<CEZLevelMapComponent>(mapUid, out var zlevel);
        snapshot = new MapComponentSnapshot(existed, zlevel.Depth, zlevel.NetworkUid, zlevel.MapAbove, zlevel.MapBelow);
        AttachMapToNetwork(network, (mapUid, zlevel), depth);

        return true;
    }

    public bool TryAddMapsIntoZNetwork(Entity<CEZLevelsNetworkComponent> network, Dictionary<EntityUid, int> maps)
    {
        var success = true;
        var addedMaps = new List<(EntityUid Uid, int Depth, MapComponentSnapshot Snapshot)>();

        foreach (var (ent, depth) in maps)
        {
            if (TryAddMapIntoZNetwork(network, ent, depth, out var snapshot))
                addedMaps.Add((ent, depth, snapshot));
            else
                success = false;
        }

        if (!success)
        {
            // Roll back only state owned by this batch. A pre-existing detached map component may
            // carry mapping metadata and must be restored rather than removed.
            foreach (var (added, _, snapshot) in addedMaps)
            {
                if (!TryComp<CEZLevelMapComponent>(added, out var zMap))
                    continue;

                DetachMapFromNetwork(network, (added, zMap));
                if (!snapshot.Existed)
                {
                    RemComp<CEZLevelMapComponent>(added);
                    continue;
                }

                zMap.Depth = snapshot.Depth;
                zMap.NetworkUid = snapshot.NetworkUid;
                zMap.MapAbove = snapshot.MapAbove;
                zMap.MapBelow = snapshot.MapBelow;
                Dirty(added, zMap);
            }

            return false;
        }

        // Deferred until the whole map batch commits. The topology reconciler observes the network
        // update below and derives connector/fallback grid linkage from the committed map set.
        foreach (var (added, depth, _) in addedMaps)
        {
            RaiseLocalEvent(added, new CEMapAddedIntoZNetworkEvent(network, depth));
        }

        RaiseLocalEvent(network, new CEZLevelNetworkUpdatedEvent());
        return true;
    }

    private readonly record struct MapComponentSnapshot(
        bool Existed,
        int Depth,
        EntityUid NetworkUid,
        EntityUid? MapAbove,
        EntityUid? MapBelow);
}

/// <summary>
/// Called on ZLevel Network Entity, when maps added or removed from network
/// </summary>
public sealed partial class CEZLevelNetworkUpdatedEvent : EntityEventArgs;

/// <summary>
/// Called on map, when it added to ZNetwork
/// </summary>
public sealed class CEMapAddedIntoZNetworkEvent(Entity<CEZLevelsNetworkComponent> network, int depth) : EntityEventArgs
{
    public Entity<CEZLevelsNetworkComponent> Network = network;
    public int Depth = depth;
}
