using Content.Shared._Onyx.Shuttles.Events;

namespace Content.Client.Shuttles.BUI;

public sealed partial class ShuttleConsoleBoundUserInterface
{
    private void InitializeDampening()
    {
        _window!.DampeningModeChanged += OnDampeningModeChanged;
    }

    private void OnDampeningModeChanged(InertiaDampeningMode mode)
    {
        SendMessage(new SetInertiaDampeningRequest { Mode = mode });
    }
}
