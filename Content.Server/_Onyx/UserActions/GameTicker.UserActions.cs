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

        if (stationNames.Length == 0)
            stationNames.Append(_gameMapManager.GetSelectedMap()?.MapName ?? Loc.GetString("game-ticker-no-map-selected"));

        RaiseNetworkEvent(new TickerInGameInfoEvent(
                _gameMapManager.GetSelectedMap()?.MapName ?? Loc.GetString("game-ticker-no-map-selected"),
                RoundId,
                Decoy == null ? Loc.GetString(preset.ModeTitle) : Loc.GetString(Decoy.ModeTitle),
                _playerManager.PlayerCount),
            Filter.Empty().AddPlayers(_playerManager.NetworkedSessions));
    }
}
