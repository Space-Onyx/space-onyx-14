using static Content.Client.Inventory.ClientInventorySystem;

namespace Content.Client.UserInterface.Controls
{
    public sealed partial class SlotButton : SlotControl // <Onyx-InventorySubslots-edited>
    {
        public SlotButton() { }

        public SlotButton(SlotData slotData)
        {
            ButtonTexturePath = slotData.TextureName;
            FullButtonTexturePath = slotData.FullTextureName;
            Blocked = slotData.Blocked;
            Highlight = slotData.Highlighted;
            StorageTexturePath = "Slots/back";
            SlotName = slotData.SlotName;

            // <Onyx-InventorySubslots>
            if (slotData.SubSlotOf != null)
            {
                var toolTip = Loc.GetString($"inventory-subslot-{slotData.SlotName}");
                ButtonRect.ToolTip = toolTip;
                StorageButton.ToolTip = toolTip;
                BlockedRect.ToolTip = toolTip;
            }
            // </Onyx-InventorySubslots>

            InitializeSubSlots(); // <Onyx-InventorySubslots>
        }
    }
}
