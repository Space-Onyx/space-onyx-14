using Content.Shared._Onyx.FireControl;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Onyx.FireControl.UI;

[UsedImplicitly]
public sealed class FireControlConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private FireControlWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<FireControlWindow>();
        _window.OnServerRefresh += () => SendMessage(new FireControlConsoleRefreshServerMessage());
        _window.Radar.OnRadarClick += coordinates =>
        {
            var selected = _window.GetSelectedWeapons();
            if (selected.Count != 0)
                SendMessage(new FireControlConsoleFireMessage(selected, EntMan.GetNetCoordinates(coordinates)));
        };
        _window.Radar.DefaultCursorShape = Control.CursorShape.Crosshair;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not FireControlConsoleBoundInterfaceState fireControl || _window == null)
            return;

        _window.UpdateStatus(fireControl);
        _window.Radar.SetConsole(Owner);
    }
}
