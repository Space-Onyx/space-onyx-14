using System.Linq;
using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Server.Database;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.Administration;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Administration.Commands;

[AdminCommand(AdminFlags.Playtime)]
public sealed partial class PlayTimeAddOverallAsyncCommand : IConsoleCommand
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private PlayTimeTrackingManager _playTimeTracking = default!;
    [Dependency] private IPlayerLocator _playerLocator = default!;
    [Dependency] private IServerDbManager _db = default!;

    public string Command => "playtime_addoverall_as";
    public string Description => Loc.GetString("cmd-playtime_addoverall-desc");
    public string Help => Loc.GetString("cmd-playtime_addoverall-help", ("command", Command));

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("cmd-playtime_addoverall-error-args"));
            return;
        }

        if (!int.TryParse(args[1], out var minutes) || minutes == 0)
        {
            shell.WriteError(Loc.GetString("parse-minutes-fail", ("minutes", args[1])));
            return;
        }

        var player = await FindPlayer(args[0]);
        if (player == null)
        {
            shell.WriteError(Loc.GetString("parse-session-fail", ("username", args[0])));
            return;
        }

        TimeSpan total;
        var delta = TimeSpan.FromMinutes(minutes);
        if (_playerManager.TryGetSessionById(player.Value, out var session))
        {
            _playTimeTracking.AddTimeToOverallPlaytime(session, delta);
            total = _playTimeTracking.GetOverallPlaytime(session);
            _playTimeTracking.SaveSession(session);
        }
        else
        {
            total = await AddTimeToDatabase(player.Value, PlayTimeTrackingShared.TrackerOverall, delta);
        }

        shell.WriteLine(Loc.GetString("cmd-playtime_addoverall-succeed",
            ("username", args[0]),
            ("time", total)));
    }

    private async Task<NetUserId?> FindPlayer(string nameOrId)
    {
        if (Guid.TryParse(nameOrId, out var guid))
            return new NetUserId(guid);

        return (await _playerLocator.LookupIdByNameAsync(nameOrId))?.UserId;
    }

    private async Task<TimeSpan> AddTimeToDatabase(NetUserId userId, string tracker, TimeSpan delta)
    {
        var times = await _db.GetPlayTimes(userId.UserId);
        var total = times.FirstOrDefault(x => x.Tracker == tracker)?.TimeSpent ?? TimeSpan.Zero;
        total += delta;
        await _db.UpdatePlayTimes([new PlayTimeUpdate(userId, tracker, total)]);
        return total;
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHintOptions(CompletionHelper.SessionNames(players: _playerManager),
                Loc.GetString("cmd-playtime_addoverall-arg-user"));
        if (args.Length == 2)
            return CompletionResult.FromHint(Loc.GetString("cmd-playtime_addoverall-arg-minutes"));
        return CompletionResult.Empty;
    }
}

[AdminCommand(AdminFlags.Playtime)]
public sealed partial class PlayTimeAddRoleAsyncCommand : IConsoleCommand
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private PlayTimeTrackingManager _playTimeTracking = default!;
    [Dependency] private IPlayerLocator _playerLocator = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    public string Command => "playtime_addrole_as";
    public string Description => Loc.GetString("cmd-playtime_addrole-desc");
    public string Help => Loc.GetString("cmd-playtime_addrole-help", ("command", Command));

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 3)
        {
            shell.WriteError(Loc.GetString("cmd-playtime_addrole-error-args"));
            return;
        }

        if (!_prototypeManager.HasIndex<PlayTimeTrackerPrototype>(args[1]))
        {
            shell.WriteError(Loc.GetString("admin-time-panel-invalid-tracker", ("tracker", args[1])));
            return;
        }

        if (!int.TryParse(args[2], out var minutes) || minutes == 0)
        {
            shell.WriteError(Loc.GetString("parse-minutes-fail", ("minutes", args[2])));
            return;
        }

        var player = await FindPlayer(args[0]);
        if (player == null)
        {
            shell.WriteError(Loc.GetString("parse-session-fail", ("username", args[0])));
            return;
        }

        TimeSpan total;
        var delta = TimeSpan.FromMinutes(minutes);
        if (_playerManager.TryGetSessionById(player.Value, out var session))
        {
            _playTimeTracking.AddTimeToTracker(session, args[1], delta);
            total = _playTimeTracking.GetPlayTimeForTracker(session, args[1]);
            _playTimeTracking.SaveSession(session);
        }
        else
        {
            var times = await _db.GetPlayTimes(player.Value.UserId);
            total = times.FirstOrDefault(x => x.Tracker == args[1])?.TimeSpent ?? TimeSpan.Zero;
            total += delta;
            await _db.UpdatePlayTimes([new PlayTimeUpdate(player.Value, args[1], total)]);
        }

        shell.WriteLine(Loc.GetString("cmd-playtime_addrole-succeed",
            ("username", args[0]),
            ("role", args[1]),
            ("time", total)));
    }

    private async Task<NetUserId?> FindPlayer(string nameOrId)
    {
        if (Guid.TryParse(nameOrId, out var guid))
            return new NetUserId(guid);

        return (await _playerLocator.LookupIdByNameAsync(nameOrId))?.UserId;
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHintOptions(CompletionHelper.SessionNames(players: _playerManager),
                Loc.GetString("cmd-playtime_addrole-arg-user"));
        if (args.Length == 2)
            return CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<PlayTimeTrackerPrototype>(),
                Loc.GetString("cmd-playtime_addrole-arg-role"));
        if (args.Length == 3)
            return CompletionResult.FromHint(Loc.GetString("cmd-playtime_addrole-arg-minutes"));
        return CompletionResult.Empty;
    }
}
