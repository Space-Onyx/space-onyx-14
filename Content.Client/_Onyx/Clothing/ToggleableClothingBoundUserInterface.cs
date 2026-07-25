using Content.Shared.Clothing.Components;
using Robust.Client.UserInterface;

namespace Content.Client._Onyx.Clothing;

public sealed partial class ToggleableClothingBoundUserInterface : BoundUserInterface
{
    private ToggleableClothingRadialMenu? _menu;

    public ToggleableClothingBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<ToggleableClothingRadialMenu>();
        _menu.SetEntity(Owner);
        _menu.SendToggleClothingMessageAction += TogglePart;
        _menu.OpenOverMouseScreenPosition();
    }

    private void TogglePart(EntityUid part)
    {
        SendPredictedMessage(new ToggleableClothingUiMessage(EntMan.GetNetEntity(part)));
        _menu?.RefreshUI();
    }
}
