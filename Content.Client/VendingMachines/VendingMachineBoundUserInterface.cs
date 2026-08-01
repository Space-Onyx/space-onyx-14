using Content.Client._Onyx.VendingMachines.UI;
using Content.Shared._Onyx.Salvage.MiningPoints;
using Content.Shared._Onyx.Materials;
using Content.Shared.VendingMachines;
using Robust.Client.UserInterface;

namespace Content.Client.VendingMachines;

public sealed class VendingMachineBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private FancyVendingMachineMenu? _menu;

    protected override void Open()
    {
        base.Open();

        _menu = EntMan.HasComponent<SalvageMiningPointVendorComponent>(Owner)
            ? new SalvageVendingMachineMenu()
            : new FancyVendingMachineMenu();
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

        _menu?.Populate(Owner, vendingState.Inventory, vendingState.PriceMultiplier, vendingState.Credits,
            vendingState.ShowWithdraw, vendingState.BalanceLabel, vendingState.InfiniteStock,
            vendingState.UsesIdCardMiningPoints);
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
