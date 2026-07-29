using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Onyx.StationEvents.SecretPlus;

[AdminCommand(AdminFlags.Admin)]
public sealed class SecretPlusStatusCommand : IConsoleCommand
{
    public string Command => "secretplusstatus";
    public string Description => "Shows SecretPlus chaos, rate, ramp and affordable event candidates.";
    public string Help => "secretplusstatus";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteError(Help);
            return;
        }

        var entities = IoCManager.Resolve<IEntityManager>();
        var lines = entities.System<SecretPlusSystem>().GetStatus().ToList();
        if (lines.Count == 0)
        {
            shell.WriteLine("No active SecretPlus scheduler.");
            return;
        }

        foreach (var line in lines)
            shell.WriteLine(line);
    }
}
