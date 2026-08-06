/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server._Onyx.ZLevels.Core;
using System.Linq;
using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Shared._Onyx.ZLevels.Mapping.Prototypes;
using Content.Shared.Administration;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Onyx.ZLevels.Mapping.Commands;

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed partial class CEMappingZNetworkCommand : LocalizedEntityCommands
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private CEZLevelsSystem _zLevel = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private MapSystem _map = default!;

    public override string Command => "znetwork-mapping";
    public override string Description => "Load CEZLevelMapPrototype as ZNetwork for mapping";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args) =>
        CompletionResult.FromHintOptions(
            _proto.EnumeratePrototypes<CEZLevelMapPrototype>().Select(map => new CompletionOption(map.ID)),
            "CEZLevelMapPrototype");

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }
        if (args.Length != 1 || !_proto.Resolve<CEZLevelMapPrototype>(args[0], out var indexedZMap))
        {
            shell.WriteError($"Unknown CEZLevelMapPrototype {args.FirstOrDefault()}");
            return;
        }

        var network = _zLevel.CreateZNetwork(indexedZMap.Components);
        _meta.SetEntityName(network, $"Mapping zNetwork: {indexedZMap.ID}");
        var maps = new Dictionary<EntityUid, int>();
        var createdMaps = new List<MapId>();
        var opts = new DeserializationOptions { StoreYamlUids = true };

        for (var depth = 0; depth < indexedZMap.Maps.Count; depth++)
        {
            var path = indexedZMap.Maps[depth];
            if (!_mapLoader.TryLoadMap(path, out var mapEnt, out _, opts))
            {
                shell.WriteError($"Failed to load zNetwork map (depth {depth}): {path}!");
                Cleanup(createdMaps, network);
                return;
            }
            maps.Add(mapEnt.Value, depth);
            createdMaps.Add(mapEnt.Value.Comp.MapId);
            _meta.SetEntityName(mapEnt.Value, $"Mapping {indexedZMap.ID} [{depth}]");
        }

        if (!_zLevel.TryAddMapsIntoZNetwork(network, maps))
        {
            shell.WriteError("Failed to create zNetwork from loaded maps!");
            Cleanup(createdMaps, network);
            return;
        }

        SetupMapping(shell, player, createdMaps, indexedZMap.ID);
    }

    private void SetupMapping(IConsoleShell shell, Robust.Shared.Player.ICommonSession player, List<MapId> maps, string id)
    {
        if (player.AttachedEntity is { Valid: true } playerEntity &&
            (EntityManager.GetComponent<MetaDataComponent>(playerEntity).EntityPrototype is not { } proto ||
             proto.ID != GameTicker.AdminObserverPrototypeName))
            shell.ExecuteCommand("aghost");

        shell.ExecuteCommand("changecvar events.enabled false");
        shell.ExecuteCommand("changecvar shuttle.auto_call_time 0");
        if (maps.Count > 0)
            shell.ExecuteCommand($"tp 0 0 {maps[0]}");
        else
            shell.WriteError($"No maps were loaded for prototype {id}; skipping teleport.");
        shell.RemoteExecuteCommand("mappingclientsidesetup");
        foreach (var mapId in maps)
            DebugTools.Assert(_map.IsPaused(mapId));
    }

    private void Cleanup(List<MapId> maps, EntityUid network)
    {
        foreach (var mapId in maps)
        {
            if (_map.MapExists(mapId))
                _map.DeleteMap(mapId);
        }
        EntityManager.QueueDeleteEntity(network);
    }
}
