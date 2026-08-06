/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server._Onyx.ZLevels.Core;
using System.Linq;
using Content.Server._Onyx.ZLevels.Core.Components;
using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Onyx.ZLevels.Mapping.Commands;

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed partial class CEGameMapMappingZNetworkCommand : LocalizedEntityCommands
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private CEZLevelsSystem _zLevel = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private MapSystem _map = default!;

    public override string Command => "znetwork-gamemap-mapping";
    public override string Description => "Load existing game map prototype as ZNetwork for mapping";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        var options = new List<CompletionOption>();
        foreach (var map in _proto.EnumeratePrototypes<GameMapPrototype>())
        {
            if (FindZNetwork(map) != null)
                options.Add(new CompletionOption(map.ID, map.MapName));
        }
        return CompletionResult.FromHintOptions(options, "GameMapPrototype with CEStationZLevelsComponent");
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }
        if (args.Length != 1 || !_proto.Resolve<GameMapPrototype>(args[0], out var mapProto))
        {
            shell.WriteError($"Unknown GameMapPrototype {args.FirstOrDefault()}");
            return;
        }

        var zNetwork = FindZNetwork(mapProto);
        if (zNetwork == null)
        {
            shell.WriteError($"No station with CEStationZLevelsComponent found in map {mapProto.ID}");
            return;
        }

        var network = _zLevel.CreateZNetwork(zNetwork.ZLevelsComponentOverrides);
        _meta.SetEntityName(network, $"Mapping zNetwork: {mapProto.MapName}");
        var maps = new Dictionary<EntityUid, int>();
        var createdMaps = new List<MapId>();
        var opts = new DeserializationOptions { StoreYamlUids = true };

        if (!_mapLoader.TryLoadMap(mapProto.MapPath, out var defaultMap, out _, opts))
        {
            shell.WriteError($"Failed to load default zNetwork map: {mapProto.MapPath}!");
            Cleanup(createdMaps, network);
            return;
        }
        maps.Add(defaultMap.Value, 0);
        createdMaps.Add(defaultMap.Value.Comp.MapId);
        _meta.SetEntityName(defaultMap.Value, $"Mapping {mapProto.MapName}");

        var depth = -zNetwork.MapsBelow.Count;
        foreach (var path in zNetwork.MapsBelow)
        {
            if (!TryLoad(path, depth, mapProto.MapName, maps, createdMaps, shell))
            {
                Cleanup(createdMaps, network);
                return;
            }
            depth++;
        }

        depth = 1;
        foreach (var path in zNetwork.MapsAbove)
        {
            if (!TryLoad(path, depth, mapProto.MapName, maps, createdMaps, shell))
            {
                Cleanup(createdMaps, network);
                return;
            }
            depth++;
        }

        if (!_zLevel.TryAddMapsIntoZNetwork(network, maps))
        {
            shell.WriteError("Failed to create zNetwork from loaded maps!");
            Cleanup(createdMaps, network);
            return;
        }

        if (player.AttachedEntity is { Valid: true } playerEntity &&
            (EntityManager.GetComponent<MetaDataComponent>(playerEntity).EntityPrototype is not { } proto ||
             proto.ID != GameTicker.AdminObserverPrototypeName))
            shell.ExecuteCommand("aghost");

        shell.ExecuteCommand("changecvar events.enabled false");
        shell.ExecuteCommand("changecvar shuttle.auto_call_time 0");
        shell.ExecuteCommand($"tp 0 0 {defaultMap.Value.Comp.MapId}");
        shell.RemoteExecuteCommand("mappingclientsidesetup");
        foreach (var mapId in createdMaps)
            DebugTools.Assert(_map.IsPaused(mapId));
    }

    private CEStationZLevelsComponent? FindZNetwork(GameMapPrototype map)
    {
        foreach (var station in map.Stations.Values)
        {
            if (station.StationComponentOverrides.TryGetComponent<CEStationZLevelsComponent>(_compFactory, out var zNetwork) &&
                (zNetwork.MapsAbove.Count > 0 || zNetwork.MapsBelow.Count > 0))
                return zNetwork;
        }
        return null;
    }

    private bool TryLoad(ResPath path,
        int depth,
        string name,
        Dictionary<EntityUid, int> maps,
        List<MapId> createdMaps,
        IConsoleShell shell)
    {
        var opts = new DeserializationOptions { StoreYamlUids = true };
        if (!_mapLoader.TryLoadMap(path, out var map, out _, opts))
        {
            shell.WriteError($"Failed to load zNetwork map (depth {depth}): {path}!");
            return false;
        }
        maps.Add(map.Value, depth);
        createdMaps.Add(map.Value.Comp.MapId);
        _meta.SetEntityName(map.Value, $"Mapping {name} [{depth}]");
        return true;
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
