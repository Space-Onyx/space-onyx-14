/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Linq;
using Content.Server._Onyx.ZLevels.Core;
using Content.Server.Administration;
using Content.Shared._Onyx.ZLevels.Core.Components;
using Content.Shared.Administration;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Utility;

namespace Content.Server._Onyx.ZLevels.Mapping.Commands;

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed partial class CEAddMapBelowZNetworkCommand : LocalizedEntityCommands
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IResourceManager _resourceMgr = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private CEZLevelsSystem _zLevel = default!;
    [Dependency] private MetaDataSystem _meta = default!;

    public override string Command => "znetwork-add-below";
    public override string Description => "Add a map below an existing z-network.";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var options = new List<CompletionOption>();
            var query = _entities.EntityQueryEnumerator<CEZLevelsNetworkComponent, MetaDataComponent>();
            while (query.MoveNext(out var uid, out _, out var meta))
                options.Add(new CompletionOption(_entities.GetNetEntity(uid).ToString(), meta.EntityName));
            return CompletionResult.FromHintOptions(options, "zNetwork net entity");
        }
        if (args.Length == 2)
        {
            var options = CompletionHelper.UserFilePath(args[1], _resourceMgr.UserData)
                .Concat(CompletionHelper.ContentFilePath(args[1], _resourceMgr));
            return CompletionResult.FromHintOptions(options, Loc.GetString("cmd-hint-mapping-path"));
        }
        return CompletionResult.Empty;
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2 || !TryGetNetwork(args[0], out var target, out var levelComp))
        {
            shell.WriteError("Invalid arguments or zNetwork.");
            return;
        }

        var path = new ResPath(args[1]);
        var opts = new DeserializationOptions { StoreYamlUids = true };
        if (!_mapLoader.TryLoadMap(path, out var mapEnt, out _, opts))
        {
            shell.WriteError($"Failed to load map: {path}!");
            return;
        }

        var newDepth = levelComp.ZLevels.Count > 0 ? levelComp.ZLevels.Keys.Min() - 1 : -1;
        if (!_zLevel.TryAddMapsIntoZNetwork((target, levelComp), new() { [mapEnt.Value] = newDepth }))
        {
            shell.WriteError($"Failed to add map to z-network at depth {newDepth}.");
            _entities.QueueDeleteEntity(mapEnt.Value);
            return;
        }

        _meta.SetEntityName(mapEnt.Value, $"{path.FilenameWithoutExtension} [{newDepth}]");
        shell.WriteLine($"Successfully added map {path.FilenameWithoutExtension} to z-network at depth {newDepth}.");
        shell.WriteLine($"Map ID: {mapEnt.Value.Comp.MapId}");
    }

    private bool TryGetNetwork(string value, out EntityUid target, out CEZLevelsNetworkComponent component)
    {
        target = default;
        component = default!;
        if (!NetEntity.TryParse(value, out var net) ||
            !_entities.TryGetEntity(net, out var uid) ||
            uid is not { } resolved ||
            !_entities.TryGetComponent<CEZLevelsNetworkComponent>(resolved, out var network))
        {
            return false;
        }

        target = resolved;
        component = network;
        return target.Valid;
    }
}
