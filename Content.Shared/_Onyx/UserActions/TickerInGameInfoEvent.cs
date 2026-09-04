using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.UserActions;

[Serializable, NetSerializable]
public sealed class TickerInGameInfoEvent(string mapName, int roundId, string gameMode, int playerCount) : EntityEventArgs
{
    public string MapName { get; } = mapName;
    public int RoundId { get; } = roundId;
    public string GameMode { get; } = gameMode;
    public int PlayerCount { get; } = playerCount;
}
