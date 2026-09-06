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

    public static TimeSpan GetRemaining(TimeSpan currentTime, TimeSpan availableAt)
    {
        var remaining = availableAt - currentTime;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }
}
