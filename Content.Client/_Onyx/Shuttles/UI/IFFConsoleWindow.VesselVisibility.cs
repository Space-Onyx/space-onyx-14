using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Shuttles.UI;

public sealed partial class IFFConsoleWindow
{
    private readonly ButtonGroup _showVesselButtonGroup = new();
    public event Action<bool>? ShowVessel;

    private void InitializeVesselVisibility()
    {
        ShowVesselOffButton.Group = _showVesselButtonGroup;
        ShowVesselOnButton.Group = _showVesselButtonGroup;
        ShowVesselOnButton.OnPressed += _ => ShowVessel?.Invoke(true);
        ShowVesselOffButton.OnPressed += _ => ShowVessel?.Invoke(false);
    }

    private void UpdateVesselVisibility(IFFConsoleBoundUserInterfaceState state)
    {
        var allowed = (state.AllowedFlags & IFFFlags.Hide) != 0;
        ShowVesselOffButton.Disabled = !allowed;
        ShowVesselOnButton.Disabled = !allowed;
        ShowVesselOffButton.Pressed = allowed && (state.Flags & IFFFlags.Hide) != 0;
        ShowVesselOnButton.Pressed = allowed && (state.Flags & IFFFlags.Hide) == 0;
    }
}
