using Content.Client._Onyx.VendingMachines.UI;
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

            _menu = new FancyVendingMachineMenu
            {
                Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName
            };

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

            _menu?.Populate(Owner, vendState.Inventory, vendState.PriceMultiplier, vendState.Credits);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
                return;

            if (_menu == null)
                return;

            _menu.OnItemSelected -= OnItemSelected;
            _menu.OnWithdraw -= OnWithdraw;
            _menu.Close();
            _menu.Dispose();
        }
    }
}
