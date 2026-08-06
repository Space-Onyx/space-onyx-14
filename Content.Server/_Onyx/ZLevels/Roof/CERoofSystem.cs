/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Linq;
using Content.Server._Onyx.ZLevels.Core;
using Content.Shared._Onyx.ZLevels.Core.Components;
using Content.Shared._Onyx.ZLevels.Core.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Onyx.ZLevels.Roof;

/// <inheritdoc/>
public sealed partial class CERoofSystem : EntitySystem
{
    [Dependency] private SharedRoofSystem _roof = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;
    [Dependency] private CEZGridConnectorSystem _gridConnectors = default!;

    private readonly HashSet<Vector2i> _roofMap = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEZLevelsNetworkComponent, CEZLevelNetworkUpdatedEvent>(OnNetworkUpdated);
        SubscribeLocalEvent<CEZGridNetworkComponent, CEZLevelGridNetworkUpdatedEvent>(OnGridNetworkUpdated);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
    }

    private void OnTileChanged(ref TileChangedEvent args)
    {
        if (args.Changes.Length == 0 ||
            !TryComp<CEZLinkedGridComponent>(args.Entity, out var linked) ||
            !TryComp<CEZLevelsNetworkComponent>(linked.ZNetwork, out var network))
        {
            return;
        }

        RecalculateNetworkRoofs((linked.ZNetwork, network), linked.LinkNetwork);
    }

    private void OnNetworkUpdated(Entity<CEZLevelsNetworkComponent> ent, ref CEZLevelNetworkUpdatedEvent args)
    {
        _gridConnectors.MarkDirty();
        RecalculateNetworkRoofs(ent, ent.Owner);
    }

    private void OnGridNetworkUpdated(Entity<CEZGridNetworkComponent> ent, ref CEZLevelGridNetworkUpdatedEvent args)
    {
        var grid = ent.Comp.Grids.FirstOrDefault();
        if (!grid.IsValid() ||
            Transform(grid).MapUid is not { } mapUid ||
            !TryComp<CEZLevelMapComponent>(mapUid, out var zMap) ||
            !TryComp<CEZLevelsNetworkComponent>(zMap.NetworkUid, out var network))
        {
            return;
        }

        RecalculateNetworkRoofs((zMap.NetworkUid, network), ent.Owner);
    }

    public void RecalculateNetworkRoofs(Entity<CEZLevelsNetworkComponent> network, EntityUid linkNetwork)
    {
        _roofMap.Clear();

        List<(EntityUid Uid, MapGridComponent Grid, int Depth)> sortedGrids = new();
        var query = AllEntityQuery<MapGridComponent, CEZLinkedGridComponent>();
        while (query.MoveNext(out var gridUid, out var grid, out var linked))
        {
            if (linked.ZNetwork == network.Owner && linked.LinkNetwork == linkNetwork)
                sortedGrids.Add((gridUid, grid, linked.Depth));
        }

        foreach (var (gridUid, grid, _) in sortedGrids.OrderByDescending(entry => entry.Depth))
        {
            var enumerator = _map.GetAllTilesEnumerator(gridUid, grid);
            var roofComp = EnsureComp<RoofComponent>(gridUid);

            while (enumerator.MoveNext(out var tileRef))
            {
                _roof.SetRoof((gridUid, grid, roofComp), tileRef.Value.GridIndices, _roofMap.Contains(tileRef.Value.GridIndices));

                var tileDef = (ContentTileDefinition) _tileDefinitions[tileRef.Value.Tile.TypeId];

                if (!tileDef.ZTransparent)
                    _roofMap.Add(tileRef.Value.GridIndices);
            }
        }
    }
}
