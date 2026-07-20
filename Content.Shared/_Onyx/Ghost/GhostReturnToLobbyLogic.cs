namespace Content.Shared._Onyx.Ghost;

public static class GhostReturnToLobbyLogic
{
    public static TimeSpan ComputeAvailableAt(TimeSpan ghostedAt, int delaySeconds)
    {
        if (delaySeconds < 0)
            delaySeconds = 0;

        return ghostedAt + TimeSpan.FromSeconds(delaySeconds);
    }

    public static bool CanReturn(TimeSpan currentTime, TimeSpan availableAt)
    {
        return currentTime >= availableAt;
    }

    public static bool IsPopulationAllowed(int onlinePlayers, int maxPlayers)
    {
        if (maxPlayers < 0)
            maxPlayers = 0;

        return onlinePlayers <= maxPlayers;
    }

    public static bool CanReturn(TimeSpan currentTime, TimeSpan availableAt, int onlinePlayers, int maxPlayers)
    {
        return CanReturn(currentTime, availableAt) && IsPopulationAllowed(onlinePlayers, maxPlayers);
    }

    public static TimeSpan GetRemaining(TimeSpan currentTime, TimeSpan availableAt)
    {
        var remaining = availableAt - currentTime;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }
}
