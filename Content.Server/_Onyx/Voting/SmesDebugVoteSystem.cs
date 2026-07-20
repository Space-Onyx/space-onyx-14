using Content.Server.Chat.Managers;
using Content.Server.Administration.Logs;
using Content.Server.GameTicking.Events;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Power.Components;
using Content.Shared.Station.Components;
using Content.Server.Voting.Managers;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.Power;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Content.Server.Voting;

namespace Content.Server._Onyx.Voting;
public sealed partial class SmesDebugVoteSystem : EntitySystem
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IVoteManager _voteManager = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
    }
    private void OnRoundStarting(RoundStartingEvent ev)
    {
        if (!_cfg.GetCVar(CCVars.VoteSmesDebugEnabled))
            return;

        var maxPlayers = _cfg.GetCVar(CCVars.VoteSmesDebugMaxPlayers);
        var playerCount = _playerManager.PlayerCount;
        if (playerCount > maxPlayers)
            return;

        CreateSmesDebugVote();
    }

    private void CreateSmesDebugVote()
    {
        var duration = TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.VoteSmesDebugTimer));
        var options = new VoteOptions
        {
            Title = Loc.GetString("vote-smes-debug-title"),
            InitiatorText = Loc.GetString("vote-smes-debug-initiator"),
            Duration = duration,
            Options =
            {
                (Loc.GetString("vote-smes-debug-yes"), "yes"),
                (Loc.GetString("vote-smes-debug-no"), "no"),
            }
        };

        var vote = _voteManager.CreateVote(options);
        _adminLogger.Add(LogType.Vote, LogImpact.High, $"SMES debug vote started automatically by server");
        vote.OnFinished += (_, args) =>
        {
            if (args.Winner is string winner && winner == "yes")
            {
                ApplyInfiniteBattery();
                _chatManager.DispatchServerAnnouncement(Loc.GetString("vote-smes-debug-success"));
                _adminLogger.Add(LogType.Vote, LogImpact.High, $"SMES debug vote passed - infinite battery applied to all stations");
            }
            else
            {
                _chatManager.DispatchServerAnnouncement(Loc.GetString("vote-smes-debug-failed"));
                _adminLogger.Add(LogType.Vote, LogImpact.Medium, $"SMES debug vote failed");
            }
        };
    }
    private void ApplyInfiniteBattery()
    {
        var stationQuery = EntityQueryEnumerator<StationDataComponent>();
        while (stationQuery.MoveNext(out var stationUid, out var stationData))
        {
            foreach (var grid in stationData.Grids)
            {
                ApplyInfiniteBatteryToGrid(grid);
            }
        }
    }

    private void ApplyInfiniteBatteryToGrid(EntityUid grid)
    {
        var xform = Transform(grid);
        var enumerator = xform.ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            if (!TryComp<PowerMonitoringDeviceComponent>(child, out var powerMonitoring))
                continue;

            if (powerMonitoring.Group != PowerMonitoringConsoleGroup.SMES)
                continue;

            var recharger = EnsureComp<BatterySelfRechargerComponent>(child);
            var battery = EnsureComp<BatteryComponent>(child);
            recharger.AutoRechargeRate = battery.MaxCharge;
            recharger.AutoRechargePauseTime = TimeSpan.Zero;
            Dirty(child, recharger);
            Dirty(child, battery);
        }
    }
}
