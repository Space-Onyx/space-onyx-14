using System.Linq;
using Content.Server.Administration;
using Content.Goobstation.Shared.MartialArts;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Goobstation.Server.MartialArts;

[AdminCommand(AdminFlags.Fun)]
public sealed partial class MartialArtCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entities = default!;

    public string Command => "martialart";
    public string Description => "Grants or removes a martial art form.";
    public string Help => "martialart <entityUid> <form|none>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2 || !_entities.TryParseNetEntity(args[0], out var target) || !_entities.EntityExists(target))
        {
            shell.WriteError(Help);
            return;
        }

        MartialArtsForms? form = null;
        if (!args[1].Equals("none", StringComparison.OrdinalIgnoreCase)
            && !Enum.TryParse<MartialArtsForms>(args[1], true, out var parsed))
        {
            shell.WriteError($"Unknown form: {args[1]}");
            return;
        }
        else if (!args[1].Equals("none", StringComparison.OrdinalIgnoreCase))
            form = Enum.Parse<MartialArtsForms>(args[1], true);

        _entities.System<SharedMartialArtsSystem>().TrySetForm(target.Value, form);
        shell.WriteLine(form?.ToString() ?? "none");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args) => args.Length switch
    {
        1 => CompletionResult.FromOptions(CompletionHelper.NetEntities(args[0], entManager: _entities)),
        2 => CompletionResult.FromOptions(Enum.GetNames<MartialArtsForms>().Append("none")),
        _ => CompletionResult.Empty,
    };
}
