// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Genetics.UI;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Onyx.Genetics.Ui;

[UsedImplicitly]
public sealed partial class DnaModifierBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private DnaModifierWindow? _window;

    public DnaModifierBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DnaModifierWindow>();

        _window.EjectButtonDisk.OnPressed += _ => SendMessage(
            new ItemSlotButtonPressedEvent(SharedDnaModifier.DiskSlotName));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        _window?.UpdateState((DnaModifierBoundUserInterfaceState)state);
    }
}
