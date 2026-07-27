using System.Linq;
using Content.Goobstation.Shared.MartialArts;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Onyx.MartialArts;

[AdminCommand(AdminFlags.Fun)]
public sealed partial class MartialArtCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entities = default!;

    public string Command => "martialart";
    public string Description => "Grants or removes a martial art.";
    public string Help => "martialart <entity> <form|none>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2
            || !NetEntity.TryParse(args[0], out var netEntity)
            || !_entities.TryGetEntity(netEntity, out var target))
        {
            shell.WriteError(Help);
            return;
        }
        MartialArtsForms? form = null;
        if (!args[1].Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<MartialArtsForms>(args[1], true, out var parsed))
            {
                shell.WriteError($"Unknown martial art: {args[1]}");
                return;
            }

            form = parsed;
        }
        if (!_entities.System<MartialArtsSystem>().TrySetForm(target.Value, form))
            shell.WriteError("Could not change martial art.");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
        => args.Length == 2
            ? CompletionResult.FromOptions(Enum.GetNames<MartialArtsForms>().Append("none"))
            : CompletionResult.Empty;
}
