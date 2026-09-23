using Content.Shared._Onyx.UserActions;

namespace Content.Client.GameTicking;

public sealed partial class ClientGameTicker
{
    public TickerInGameInfoEvent? UserActionsInfo { get; private set; }
    public event Action? UserActionsInfoUpdated;

    private void OnUserActionsInfo(TickerInGameInfoEvent message)
    {
        UserActionsInfo = message;
        UserActionsInfoUpdated?.Invoke();
    }
}
