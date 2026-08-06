/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Linq;
using Content.Server.Administration;
using Content.Shared._Onyx.ZLevels.Core.Components;
using Content.Shared._Onyx.ZLevels.Weather;
using Content.Shared.Administration;
using Content.Shared.Prototypes;
using Content.Shared.Weather;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.ZLevels.Weather;

[AdminCommand(AdminFlags.Fun)]
public sealed partial class CEWeatherCommand : LocalizedCommands
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;

    public override string Command => "znetwork-weather";
    public override string Description => "Sets weather for all maps in zNetwork";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2 || args.Length > 3)
        {
            shell.WriteError(Loc.GetString("cmd-weather-error-no-arguments"));
            return;
        }

        // get the target
        EntityUid? target;

        if (!NetEntity.TryParse(args[0], out var targetNet) ||
            !_entities.TryGetEntity(targetNet, out target))
        {
            shell.WriteError($"Unable to find entity {args[0]}");
            return;
        }

        if (!_entities.TryGetComponent<CEZLevelsNetworkComponent>(target, out var levelComp))
        {
            shell.WriteError($"Target entity doesnt have CEZLevelsNetworkComponent {args[0]}");
            return;
        }

        //Weather Proto parsing
        EntProtoId? weather = null;
        if (!args[1].Equals("null"))
        {
            weather = args[1];
            if (!_proto.TryIndex(weather, out var weatherProto) ||
                !weatherProto.HasComponent<WeatherStatusEffectComponent>(_componentFactory))
            {
                shell.WriteError(Loc.GetString("cmd-weather-error-unknown-proto"));
                return;
            }
        }

        //Time parsing
        TimeSpan? endTime = null;
        if (args.Length == 3)
        {
            if (!int.TryParse(args[2], out var durationInt) || durationInt <= 0)
            {
                shell.WriteError(Loc.GetString("cmd-weather-error-wrong-time"));
                return;
            }

            endTime = TimeSpan.FromSeconds(durationInt);
        }

        _entities.System<CEWeatherSystem>().SetWeather((target.Value, levelComp), weather, endTime);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var options = new List<CompletionOption>();
            var query = _entities.EntityQueryEnumerator<CEZLevelsNetworkComponent, MetaDataComponent>();
            while (query.MoveNext(out var uid, out _, out var meta))
            {
                options.Add(new CompletionOption(_entities.GetNetEntity(uid).ToString(), meta.EntityName));
            }
            return CompletionResult.FromHintOptions(options, "zNetwork net entity");
        }

        if (args.Length == 2)
        {
            var a = _proto.EnumeratePrototypes<EntityPrototype>()
                .Where(proto => proto.HasComponent<WeatherStatusEffectComponent>(_componentFactory))
                .Select(proto => new CompletionOption(proto.ID, proto.Name));
            var b = a.Concat(new[] { new CompletionOption("null", Loc.GetString("cmd-weather-null")) });
            return CompletionResult.FromHintOptions(b, Loc.GetString("cmd-weather-hint"));
        }

        if (args.Length == 3)
        {
            return CompletionResult.FromHint("Duration in seconds");
        }

        return CompletionResult.Empty;
    }
}
