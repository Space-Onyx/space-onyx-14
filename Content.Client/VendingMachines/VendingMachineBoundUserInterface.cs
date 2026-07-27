using Content.Client._Onyx.VendingMachines.UI;
using Content.Shared._Onyx.Materials;
using Content.Shared.VendingMachines;
using Robust.Client.UserInterface;

namespace Content.Client.VendingMachines
{
    public sealed class VendingMachineBoundUserInterface : BoundUserInterface
    {
        [ViewVariables]
        private FancyVendingMachineMenu? _menu;

        public VendingMachineBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

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

            if (state is not VendingMachineInterfaceState vendState)
                return;

            _menu?.Populate(Owner, vendState.Inventory, vendState.PriceMultiplier, vendState.Credits,
                vendState.ShowWithdraw, vendState.BalanceLabel, vendState.InfiniteStock,
                vendState.UsesIdCardMiningPoints); // <Onyx-SalvageVendorCatalog-edited>
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
                return;

            if (_menu == null)
                return;

            _menu.OnClose -= Close;
            _menu.OnItemSelected -= OnItemSelected;
            _menu.OnWithdraw -= OnWithdraw;
            _menu.Close();
            _menu.Dispose();
        }
    }
}
