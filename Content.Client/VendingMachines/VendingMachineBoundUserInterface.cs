using Content.Client._Onyx.VendingMachines.UI; // <Onyx-Bitrunning>
using Content.Shared._Onyx.Materials;
using Content.Shared.VendingMachines;
using Content.Shared.VendingMachines.Components;
using Robust.Client.UserInterface;

namespace Content.Client.VendingMachines;

public sealed class VendingMachineBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private FancyVendingMachineMenu? _menu;

    protected override void Open()
    {
        base.Open();

        // <Onyx-Bitrunning>
        _menu = EntMan.HasComponent<SalvageMiningPointVendorComponent>(Owner)
            ? new SalvageVendingMachineMenu()
            : new FancyVendingMachineMenu();
        // </Onyx-Bitrunning>
        _menu.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _menu.OnClose += Close;
        _menu.OnItemSelected += OnItemSelected;
        _menu.OnWithdraw += OnWithdraw;
        _menu.OpenCentered();
    }

    private void OnItemSelected(VendingMachineInventoryEntry entry)
    {
        SendPredictedMessage(new VendingMachineEjectCountMessage(entry, 1));
    }

    private void OnWithdraw()
    {
        SendMessage(new VendingMachineWithdrawMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not VendingMachineInterfaceState vendingState)
            return;

        // <Onyx-Bitrunning>
        _menu?.Populate(Owner, vendingState.Inventory, vendingState.PriceMultiplier, vendingState.Credits,
            vendingState.ShowWithdraw, vendingState.BalanceLabel, vendingState.InfiniteStock,
            vendingState.UsesIdCardMiningPoints, vendingState.UsesBitrunningPoints);
        // </Onyx-Bitrunning>
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing || _menu == null)
            return;

        _menu.OnClose -= Close;
        _menu.OnItemSelected -= OnItemSelected;
        _menu.OnWithdraw -= OnWithdraw;
        _menu.Close();
        _menu.Dispose();
    }
}
