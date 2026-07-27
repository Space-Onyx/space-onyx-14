using Content.Shared._Onyx.Salvage.MarkerBeacon;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Onyx.Salvage.MarkerBeacon;

[UsedImplicitly]
public sealed class MarkerBeaconColorPickerBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private MarkerBeaconColorPickerWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<MarkerBeaconColorPickerWindow>();
        if (EntMan.TryGetComponent<MarkerBeaconColorPickerComponent>(Owner, out var picker))
            _window.SetState(picker.ActivatedColor, picker.Hacked);
        _window.OnConfirm += (color, hacked) =>
        {
            SendPredictedMessage(new MarkerBeaconColorChangedMessage(color));
            SendPredictedMessage(new MarkerBeaconHackedChangedMessage(hacked));
        };
    }
}
