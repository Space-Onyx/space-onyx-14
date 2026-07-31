using Content.Server.Administration;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Onyx.Administration.TimeTransfer;

[AdminCommand(AdminFlags.Playtime)]
public sealed partial class TimeTransferPanelCommand : LocalizedCommands
{
    [Dependency] private EuiManager _euis = default!;

    public override string Command => "timetransferpanel";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        _euis.OpenEui(new TimeTransferPanelEui(), player);
    }
}
