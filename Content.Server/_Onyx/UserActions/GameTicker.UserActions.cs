using System.Text;
using Content.Server.Station.Components;
using Content.Shared._Onyx.UserActions;
using Robust.Shared.Player;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    private void SendUserActionsInfo()
    {
        var preset = CurrentPreset ?? Preset;
        if (preset == null)
            return;

        var stationNames = new StringBuilder();
        var query = EntityQueryEnumerator<StationJobsComponent, StationSpawningComponent, MetaDataComponent>();

        while (query.MoveNext(out _, out _, out var meta))
        {
            if (stationNames.Length > 0)
                stationNames.Append('\n');

            stationNames.Append(meta.EntityName);
        }

        if (stationNames.Length == 0)
            stationNames.Append(_gameMapManager.GetSelectedMap()?.MapName ?? Loc.GetString("game-ticker-no-map-selected"));

        RaiseNetworkEvent(new TickerInGameInfoEvent(
                stationNames.ToString(),
                RoundId,
                Decoy == null ? Loc.GetString(preset.ModeTitle) : Loc.GetString(Decoy.ModeTitle),
                _playerManager.PlayerCount),
            Filter.Empty().AddPlayers(_playerManager.NetworkedSessions));
    }
}
