using Robust.Shared.Serialization;

namespace Content.Shared.VendingMachines;

[Serializable, NetSerializable]
public sealed class VendingMachineEjectMessage(InventoryType type, string id) : BoundUserInterfaceMessage
{
    public readonly InventoryType Type = type;
    public readonly string ID = id;
}

    [Serializable, NetSerializable]
    public sealed class VendingMachineEjectCountMessage : BoundUserInterfaceMessage
    {
        public readonly VendingMachineInventoryEntry Entry;
        public readonly int Count;
        public VendingMachineEjectCountMessage(VendingMachineInventoryEntry entry, int count)
        {
            Entry = entry;
            Count = count;
        }
    }

    [Serializable, NetSerializable]
    public sealed class VendingMachineWithdrawMessage : BoundUserInterfaceMessage
    {
    }

    [Serializable, NetSerializable]
    public sealed class VendingMachineInterfaceState : BoundUserInterfaceState
    {
        public List<VendingMachineInventoryEntry> Inventory;
        public readonly double PriceMultiplier;
        public readonly int Credits;

        // <Onyx-SalvageVendorCatalog>
        public readonly bool ShowWithdraw;
        public readonly string BalanceLabel;
        public readonly bool InfiniteStock;
        public readonly bool UsesIdCardMiningPoints;
        // </Onyx-SalvageVendorCatalog>

        // <Onyx-SalvageVendorCatalog-edited>
        public VendingMachineInterfaceState(List<VendingMachineInventoryEntry> inventory, double priceMultiplier, int credits,
            bool showWithdraw, string balanceLabel, bool infiniteStock, bool usesIdCardMiningPoints = false)
        // </Onyx-SalvageVendorCatalog-edited>
        {
            Inventory = inventory;
            PriceMultiplier = priceMultiplier;
            Credits = credits;
            // <Onyx-SalvageVendorCatalog>
            ShowWithdraw = showWithdraw;
            BalanceLabel = balanceLabel;
            InfiniteStock = infiniteStock;
            UsesIdCardMiningPoints = usesIdCardMiningPoints;
            // </Onyx-SalvageVendorCatalog>
        }
    }

[Serializable, NetSerializable]
public enum VendingMachineUiKey
{
    Key
}
