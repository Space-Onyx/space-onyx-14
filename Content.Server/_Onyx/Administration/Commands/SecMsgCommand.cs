using System.Linq;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Popups;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Server._Onyx.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class SecMsgCommand : LocalizedCommands
{
    private const int MaxTargets = 20;

    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;

    private static readonly string[] PopupTypeOptions = Enum.GetNames<PopupType>();

    public override string Command => "secmsg";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHint(Loc.GetString("secmsg-command-arg-message"));

        if (args.Length == 2)
        {
            var options = PopupTypeOptions.Append("All").Concat(_playerManager.Sessions.Select(session => session.Name));
            return CompletionResult.FromHintOptions(options, Loc.GetString("secmsg-command-arg-target-or-type"));
        }

        if (args.Length >= 3)
        {
            var options = _playerManager.Sessions.Select(session => session.Name);
            return CompletionResult.FromHintOptions(options,
                Loc.GetString("secmsg-command-arg-target-n", ("target", args.Length - 2)));
        }

        return CompletionResult.Empty;
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError(Loc.GetString("secmsg-command-error-args"));
            return;
        }

        var message = args[0];
        if (string.IsNullOrWhiteSpace(message))
        {
            shell.WriteError(Loc.GetString("secmsg-command-error-empty-message"));
            return;
        }

        var popupType = PopupType.Large;
        var targetStartIndex = 1;
        if (Enum.TryParse<PopupType>(args[1], true, out var parsedType))
        {
            popupType = parsedType;
            targetStartIndex = 2;
        }

        if (targetStartIndex >= args.Length)
        {
            shell.WriteError(Loc.GetString("secmsg-command-error-no-targets"));
            return;
        }

        var allPlayers = args[targetStartIndex].Equals("All", StringComparison.OrdinalIgnoreCase);
        if (allPlayers && args.Length != targetStartIndex + 1)
        {
            shell.WriteError(Loc.GetString("secmsg-command-error-all-with-extra"));
            return;
        }

        if (!allPlayers && args.Length - targetStartIndex > MaxTargets)
        {
            shell.WriteError(Loc.GetString("secmsg-command-error-too-many-targets", ("max", MaxTargets)));
            return;
        }

        var targets = new HashSet<ICommonSession>();
        if (allPlayers)
        {
            targets.UnionWith(_playerManager.Sessions);
        }
        else
        {
            for (var i = targetStartIndex; i < args.Length; i++)
            {
                var username = args[i];
                if (!_playerManager.TryGetSessionByUsername(username, out var session))
                {
                    shell.WriteError(Loc.GetString("secmsg-command-error-player-not-found", ("username", username)));
                    continue;
                }

                targets.Add(session);
            }
        }

        var deliveredTargets = new List<ICommonSession>();
        var popup = _entityManager.System<PopupSystem>();
        foreach (var target in targets)
        {
            if (target.AttachedEntity is not { } entity)
                continue;

            popup.PopupEntity(message, entity, target, popupType);
            deliveredTargets.Add(target);
        }

        if (deliveredTargets.Count == 0)
        {
            shell.WriteError(Loc.GetString("secmsg-command-error-no-valid-targets"));
            return;
        }

        var senderName = shell.Player?.Name ?? "An administrator";
        var targetNames = allPlayers ? "all attached players" : string.Join(", ", deliveredTargets.Select(target => target.Name));
        _adminLogger.Add(LogType.AdminMessage,
            LogImpact.Low,
            $"{senderName} sent security message to {targetNames}: {message}");

        shell.WriteLine(Loc.GetString("secmsg-command-success", ("count", deliveredTargets.Count)));
    }
}
