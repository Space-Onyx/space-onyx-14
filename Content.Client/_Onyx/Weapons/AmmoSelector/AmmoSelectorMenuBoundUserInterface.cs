using Content.Shared._Onyx.Weapons.AmmoSelector;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._Onyx.Weapons.AmmoSelector;

[UsedImplicitly]
public sealed partial class AmmoSelectorMenuBoundUserInterface : BoundUserInterface
{
    [Dependency] private IClyde _display = default!;
    [Dependency] private IInputManager _input = default!;

    private AmmoSelectorMenu? _menu;

    public AmmoSelectorMenuBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<AmmoSelectorMenu>();
        _menu.SetEntity(Owner);
        _menu.AmmoSelected += SelectAmmo;
        _menu.OpenCenteredAt(_input.MouseScreenPosition.Position / _display.ScreenSize);
    }

    private void SelectAmmo(ProtoId<SelectableAmmoPrototype> id)
    {
        SendPredictedMessage(new AmmoSelectedMessage(id));
    }
}
