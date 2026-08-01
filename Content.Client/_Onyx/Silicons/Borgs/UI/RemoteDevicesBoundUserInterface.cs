using Content.Shared._Onyx.Silicons.Borgs.Components;
using Robust.Client.UserInterface;

namespace Content.Client._Onyx.Silicons.Borgs.UI;

public sealed class RemoteDevicesBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private RemoteDevicesMenu? _menu;

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<RemoteDevicesMenu>();
        _menu.OnRemoteDeviceAction += action => SendMessage(new AiRemoteControllerComponent.RemoteDeviceActionMessage(action));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is RemoteDevicesBuiState remoteState)
            _menu?.Update(remoteState);
    }
}
