/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server._Onyx.ZLevels.Core;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.Map;

namespace Content.Server._Onyx.ZLevels.Mapping.Commands;

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed partial class CECombineZNetworkCommand : LocalizedEntityCommands
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private CEZLevelsSystem _zLevels = default!;
    [Dependency] private MetaDataSystem _meta = default!;

    public override string Command => "znetwork-combine";
    public override string Description => "Connects maps into a common z-level network";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args) =>
        CompletionResult.FromHintOptions(CompletionHelper.MapIds(_entities), "Map Id in order from ground to sky");

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError("Not enough maps to form a network of levels");
            return;
        }

        var maps = new List<MapId>();
        foreach (var arg in args)
        {
            if (!int.TryParse(arg, out var value))
            {
                shell.WriteError($"Cannot parse `{arg}` into mapId");
                return;
            }

            var mapId = new MapId(value);
            if (mapId == MapId.Nullspace || !_map.MapExists(mapId) || maps.Contains(mapId))
            {
                shell.WriteError($"Invalid, missing, or duplicate map: {mapId}");
                return;
            }
            maps.Add(mapId);
        }

        var network = _zLevels.CreateZNetwork();
        _meta.SetEntityName(network, $"Combined zNetwork: {network.Owner.Id}");
        var dict = new Dictionary<EntityUid, int>();
        for (var depth = 0; depth < maps.Count; depth++)
            dict.Add(_map.GetMap(maps[depth]), depth);

        if (_zLevels.TryAddMapsIntoZNetwork(network, dict))
            shell.WriteLine($"Created z-level network! Z-Network entity: {network}");
        else
        {
            _entities.QueueDeleteEntity(network);
            shell.WriteError("Failed to combine maps into a z-network; the network entity was cleaned up.");
        }
    }
}
