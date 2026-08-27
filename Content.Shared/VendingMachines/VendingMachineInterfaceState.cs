using Robust.Shared.GameStates;
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
public sealed class VendingMachineWithdrawMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class VendingMachineInterfaceState : BoundUserInterfaceState
{
    public List<VendingMachineInventoryEntry> Inventory;
    public readonly double PriceMultiplier;
    public readonly int Credits;
    public readonly bool ShowWithdraw;
    public readonly string BalanceLabel;
    public readonly bool InfiniteStock;
    public readonly bool UsesIdCardMiningPoints;
    public readonly bool UsesBitrunningPoints; // <Onyx-Bitrunning>

    public VendingMachineInterfaceState(List<VendingMachineInventoryEntry> inventory, double priceMultiplier, int credits,
        bool showWithdraw, string balanceLabel, bool infiniteStock, bool usesIdCardMiningPoints = false,
        bool usesBitrunningPoints = false) // <Onyx-Bitrunning-edited>
    {
        Inventory = inventory;
        PriceMultiplier = priceMultiplier;
        Credits = credits;
        ShowWithdraw = showWithdraw;
        BalanceLabel = balanceLabel;
        InfiniteStock = infiniteStock;
        UsesIdCardMiningPoints = usesIdCardMiningPoints;
        UsesBitrunningPoints = usesBitrunningPoints; // <Onyx-Bitrunning>
    }
}

[Serializable, NetSerializable, DataDefinition]
public sealed partial class VendingMachineInventoryEntry
{
    [DataField]
    public InventoryType Type;

    [DataField]
    public string ID;

    [DataField]
    public uint Amount;

    [ViewVariables(VVAccess.ReadWrite)]
    public int Price;

    [DataField]
    public string? Category;

    [DataField]
    public string? OverrideName;

    [DataField]
    public int Order;

    public VendingMachineInventoryEntry() : this(InventoryType.Regular, string.Empty, 0, 0)
    {
    }

    public VendingMachineInventoryEntry(InventoryType type, string id, uint amount, int price = 0,
        string? category = null, string? overrideName = null, int order = 0)
    {
        Type = type;
        ID = id;
        Amount = amount;
        Price = price;
        Category = category;
        OverrideName = overrideName;
        Order = order;
    }

    public VendingMachineInventoryEntry(VendingMachineInventoryEntry entry)
        : this(entry.Type, entry.ID, entry.Amount, entry.Price, entry.Category, entry.OverrideName, entry.Order)
    {
    }
}

[Serializable, NetSerializable]
public enum InventoryType : byte
{
    Regular,
    Emagged,
    Contraband
}

[Serializable, NetSerializable]
public sealed class VendingMachineComponentState : ComponentState
{
    public Dictionary<string, VendingMachineInventoryEntry> Inventory = new();
    public Dictionary<string, VendingMachineInventoryEntry> EmaggedInventory = new();
    public Dictionary<string, VendingMachineInventoryEntry> ContrabandInventory = new();
    public bool Contraband;
    public bool Broken;
    public bool AllForFree;
    public Color UiButtonBorderColor;
    public Color UiButtonBaseColor;
    public Color UiButtonHoveredColor;
    public Color UiButtonDisabledColor;
}

[Serializable, NetSerializable]
public enum VendingMachineUiKey
{
    Key
}
