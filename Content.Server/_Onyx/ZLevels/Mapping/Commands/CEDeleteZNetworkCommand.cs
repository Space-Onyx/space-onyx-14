/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server.Administration;
using Content.Shared._Onyx.ZLevels.Core.Components;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Onyx.ZLevels.Mapping.Commands;

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed partial class CEDeleteZNetworkCommand : LocalizedEntityCommands
{
    [Dependency] private IEntityManager _entities = default!;

    public override string Command => "znetwork-delete";
    public override string Description => "Delete all maps into selected zNetwork + zNetwork entity";

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
            shell.WriteError("Wrong arguments count.");
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
            if (mapUid.HasValue)
                _entities.QueueDeleteEntity(mapUid.Value);
        }

        _entities.QueueDeleteEntity(target.Value);
        shell.WriteLine("ZNetwork and all its maps deleted.");
    }
}
