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

    protected override void Open()
    {
        base.Open();
        var menu = this.CreateWindow<AugmentToolPanelMenu>();
        menu.SetEntity(Owner);
        menu.ToolSelected += tool => SendMessage(new AugmentToolPanelSwitchMessage(_entities.GetNetEntity(tool)));
        menu.OpenCenteredAt(_input.MouseScreenPosition.Position / _clyde.ScreenSize);
    }
}
