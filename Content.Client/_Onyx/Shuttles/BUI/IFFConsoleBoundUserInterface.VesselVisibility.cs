using Content.Shared.Shuttles.Events;

namespace Content.Client.Shuttles.BUI;

public sealed partial class IFFConsoleBoundUserInterface
{
    private void InitializeVesselVisibility()
    {
        _window!.ShowVessel += show => SendMessage(new IFFShowVesselMessage { Show = show });
    }
}
