using Robust.Shared.Serialization;

namespace Content.Shared.VendingMachines
{
    [Serializable, NetSerializable]
    public sealed class VendingMachineEjectMessage : BoundUserInterfaceMessage
    {
        public readonly InventoryType Type;
        public readonly string ID;
        public VendingMachineEjectMessage(InventoryType type, string id)
        {
            Type = type;
            ID = id;
        }
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

        public VendingMachineInterfaceState(List<VendingMachineInventoryEntry> inventory, double priceMultiplier, int credits)
        {
            Inventory = inventory;
            PriceMultiplier = priceMultiplier;
            Credits = credits;
        }
    }

    [Serializable, NetSerializable]
    public enum VendingMachineUiKey
    {
        Key,
    }
}
