using Content.Shared._Onyx.Salvage;
using Robust.Client.UserInterface;

namespace Content.Client._Onyx.Salvage.UI;

public sealed class MiningVoucherBoundUserInterface : BoundUserInterface
{
    public MiningVoucherBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        var menu = this.CreateWindow<MiningVoucherMenu>();
        menu.SetEntity(Owner);
        menu.OnSelected += index =>
        {
            SendMessage(new MiningVoucherSelectMessage(index));
            Close();
        };
    }
}
