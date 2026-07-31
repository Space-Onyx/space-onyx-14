using Content.Shared._Onyx.Shuttles.Events;

namespace Content.Client.Shuttles.UI;

public sealed partial class ShuttleConsoleWindow
{
    public event Action<InertiaDampeningMode>? DampeningModeChanged;

    private void InitializeDampening()
    {
        NavContainer.DampeningModeChanged += mode => DampeningModeChanged?.Invoke(mode);
    }
}
