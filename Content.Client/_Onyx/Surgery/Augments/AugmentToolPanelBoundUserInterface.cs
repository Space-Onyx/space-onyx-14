using System.Linq;
using Content.Shared._Onyx.Surgery.Augments;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;

namespace Content.Client._Onyx.Surgery.Augments;

public sealed partial class AugmentToolPanelBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IEntityManager _entities = default!;

    private AugmentToolPanelMenu? _menu;

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<AugmentToolPanelMenu>();
        _menu.ToolSelected += tool => SendMessage(new AugmentToolPanelSwitchMessage(_entities.GetNetEntity(tool)));
        if (State is AugmentToolPanelBuiState state)
            _menu.SetTools(state.Tools.Select(tool => _entities.GetEntity(tool)));
        _menu.OpenCenteredAt(_input.MouseScreenPosition.Position / _clyde.ScreenSize);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is AugmentToolPanelBuiState panelState)
            _menu?.SetTools(panelState.Tools.Select(tool => _entities.GetEntity(tool)));
    }
}
