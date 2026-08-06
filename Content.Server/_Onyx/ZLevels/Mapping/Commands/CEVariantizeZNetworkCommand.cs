/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server.Administration;
using Content.Shared._Onyx.ZLevels.Core.Components;
using Content.Shared.Administration;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Onyx.ZLevels.Mapping.Commands;

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed partial class CEVariantizeZNetworkCommand : LocalizedEntityCommands
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private TileSystem _tile = default!;
    [Dependency] private TurfSystem _turf = default!;

    public override string Command => "znetwork-variantize";
    public override string Description => "Random tile variations over all zNetwork maps";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        var options = new List<CompletionOption>();
        var query = _entities.EntityQueryEnumerator<CEZLevelsNetworkComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out _, out var meta))
            options.Add(new CompletionOption(_entities.GetNetEntity(uid).ToString(), meta.EntityName));
        return CompletionResult.FromHintOptions(options, "zNetwork net entity");
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var targetNet) ||
            !_entities.TryGetEntity(targetNet, out var target) ||
            !_entities.TryGetComponent<CEZLevelsNetworkComponent>(target, out var levelComp))
        {
            shell.WriteError($"Unable to find zNetwork {args[0]}");
            return;
        }

        foreach (var mapUid in levelComp.ZLevels.Values)
        {
            if (!mapUid.HasValue || !_entities.TryGetComponent<MapComponent>(mapUid.Value, out var mapComp))
            {
                shell.WriteError($"Euid '{mapUid}' is missing MapComponent.");
                continue;
            }

            foreach (var grid in _map.GetAllGrids(mapComp.MapId))
            foreach (var tile in _map.GetAllTiles(grid.Owner, grid.Comp))
            {
                var def = _turf.GetContentTileDefinition(tile);
                var newTile = new Tile(tile.Tile.TypeId, tile.Tile.Flags, _tile.PickVariant(def), tile.Tile.RotationMirroring);
                _map.SetTile(grid.Owner, grid.Comp, tile.GridIndices, newTile);
            }
        }
    }
}
