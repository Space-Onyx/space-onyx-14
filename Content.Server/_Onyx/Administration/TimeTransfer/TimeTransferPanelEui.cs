using System.Text.RegularExpressions;
using System.Linq;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Content.Shared._Onyx.Administration.TimeTransfer;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Server.Player;

namespace Content.Server._Onyx.Administration.TimeTransfer;

public sealed partial class TimeTransferPanelEui : BaseEui
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private ILogManager _log = default!;
    [Dependency] private IPlayerLocator _players = default!;
    [Dependency] private IServerDbManager _database = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private PlayTimeTrackingManager _playTime = default!;

    private readonly ISawmill _sawmill;

    public TimeTransferPanelEui()
    {
        IoCManager.InjectDependencies(this);
        _sawmill = _log.GetSawmill("admin.time_eui");
    }

    public override TimeTransferPanelEuiState GetNewState() =>
        new(_admin.HasAdminFlag(Player, AdminFlags.Playtime));

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        if (msg is TimeTransferEuiMessage message)
            TransferTime(message);
    }

    private async void TransferTime(TimeTransferEuiMessage message)
    {
        if (!_admin.HasAdminFlag(Player, AdminFlags.Playtime))
        {
            _sawmill.Warning($"{Player.Name} ({Player.UserId}) tried to transfer playtime without the playtime flag");
            return;
        }

        if (message.TimeData.Count == 0 || message.TimeData.Any(data =>
                !_prototypes.HasIndex<PlayTimeTrackerPrototype>(data.PlaytimeTracker) || !TryParseMinutes(data.TimeString, out _)))
        {
            SendMessage(new TimeTransferWarningEuiMessage(Loc.GetString("time-transfer-panel-warning-invalid-time"), Color.Red));
            return;
        }

        var player = await _players.LookupIdByNameOrIdAsync(message.PlayerId);
        if (player == null)
        {
            _sawmill.Warning($"{Player.Name} ({Player.UserId}) tried to transfer playtime to unknown player {message.PlayerId}");
            SendMessage(new TimeTransferWarningEuiMessage(Loc.GetString("time-transfer-panel-no-player-database-message"), Color.Red));
            return;
        }

        if (_playerManager.TryGetSessionById(player.UserId, out var session))
        {
            _playTime.FlushTracker(session);
            var liveTimes = _playTime.GetPlayTimes(session);
            foreach (var data in message.TimeData)
            {
                TryParseMinutes(data.TimeString, out var minutes);
                var requested = TimeSpan.FromMinutes(minutes);
                var delta = message.Overwrite
                    ? requested - liveTimes.GetValueOrDefault(data.PlaytimeTracker)
                    : requested;
                _playTime.AddTimeToTracker(session, data.PlaytimeTracker, delta);
            }

            _playTime.QueueSendTimers(session);
            _playTime.SaveSession(session);
            SendSuccess(message, message.TimeData.Count, player.UserId);
            return;
        }

        var storedTimes = message.Overwrite
            ? new Dictionary<string, TimeSpan>()
            : (await _database.GetPlayTimes(player.UserId.UserId)).ToDictionary(entry => entry.Tracker, entry => entry.TimeSpent);
        var updates = new List<PlayTimeUpdate>();
        foreach (var data in message.TimeData)
        {
            TryParseMinutes(data.TimeString, out var minutes);
            var time = TimeSpan.FromMinutes(minutes) + storedTimes.GetValueOrDefault(data.PlaytimeTracker);
            updates.Add(new PlayTimeUpdate(player.UserId, data.PlaytimeTracker, time));
        }

        await _database.UpdatePlayTimes(updates);
        SendSuccess(message, updates.Count, player.UserId);
    }

    private void SendSuccess(TimeTransferEuiMessage message, int trackerCount, NetUserId userId)
    {
        _sawmill.Info($"{Player.Name} ({Player.UserId}) {(message.Overwrite ? "set" : "added")} {trackerCount} playtime trackers for {userId}");
        SendMessage(new TimeTransferWarningEuiMessage(
            Loc.GetString(message.Overwrite ? "time-transfer-panel-warning-set-success" : "time-transfer-panel-warning-add-success"),
            Color.LightGreen));
    }

    private static bool TryParseMinutes(string value, out int minutes)
    {
        if (int.TryParse(value, out minutes))
            return minutes != 0;

        minutes = 0;
        var position = 0;
        var units = new Dictionary<string, int> { ["y"] = 525960, ["mo"] = 43800, ["w"] = 10080, ["d"] = 1440, ["h"] = 60, ["m"] = 1 };
        foreach (Match match in TimePattern().Matches(value))
        {
            if (match.Index != position || !int.TryParse(match.Groups[1].Value, out var amount))
                return false;
            minutes += amount * units[match.Groups[2].Value.ToLowerInvariant()];
            position += match.Length;
        }
        return position == value.Length && minutes != 0;
    }

    [GeneratedRegex("(-?\\d+)(mo|[ywdhm])", RegexOptions.IgnoreCase)]
    private static partial Regex TimePattern();

    public override async void Opened()
    {
        base.Opened();
        _admin.OnPermsChanged += OnPermsChanged;
    }

    public override void Closed()
    {
        base.Closed();
        _admin.OnPermsChanged -= OnPermsChanged;
    }

    private void OnPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player == Player)
            StateDirty();
    }
}
