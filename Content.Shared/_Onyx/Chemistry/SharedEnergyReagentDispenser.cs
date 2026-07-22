using Content.Shared.Chemistry;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Chemistry;

public static class SharedEnergyReagentDispenser
{
    public const string OutputSlotName = "energyBeakerSlot";
}

[Serializable, NetSerializable]
public sealed class EnergyReagentDispenserSetDispenseAmountMessage(string amount) : BoundUserInterfaceMessage
{
    public readonly EnergyReagentDispenserDispenseAmount Amount = amount switch
    {
        "1" => EnergyReagentDispenserDispenseAmount.U1,
        "5" => EnergyReagentDispenserDispenseAmount.U5,
        "10" => EnergyReagentDispenserDispenseAmount.U10,
        "15" => EnergyReagentDispenserDispenseAmount.U15,
        "20" => EnergyReagentDispenserDispenseAmount.U20,
        "25" => EnergyReagentDispenserDispenseAmount.U25,
        "30" => EnergyReagentDispenserDispenseAmount.U30,
        "50" => EnergyReagentDispenserDispenseAmount.U50,
        "100" => EnergyReagentDispenserDispenseAmount.U100,
        _ => throw new ArgumentException($"Invalid dispense amount: {amount}")
    };
}

[Serializable, NetSerializable]
public sealed class EnergyReagentDispenserDispenseReagentMessage(string reagentId) : BoundUserInterfaceMessage
{
    public readonly string ReagentId = reagentId;
}

[Serializable, NetSerializable]
public sealed class EnergyReagentDispenserClearContainerSolutionMessage : BoundUserInterfaceMessage;

public enum EnergyReagentDispenserDispenseAmount { U1 = 1, U5 = 5, U10 = 10, U15 = 15, U20 = 20, U25 = 25, U30 = 30, U50 = 50, U100 = 100 }

[Serializable, NetSerializable]
public sealed class EnergyReagentInventoryItem(string id, string label, float cost, Color color)
{
    public string Id = id;
    public string Label = label;
    public float Cost = cost;
    public Color Color = color;
}

[Serializable, NetSerializable]
public sealed class EnergyReagentDispenserBoundUserInterfaceState(
    ContainerInfo? outputContainer,
    NetEntity? outputContainerEntity,
    List<EnergyReagentInventoryItem> inventory,
    EnergyReagentDispenserDispenseAmount amount,
    float charge,
    float maxCharge,
    float receiving,
    float idleUse,
    bool usingBattery,
    bool hasPower) : BoundUserInterfaceState
{
    public readonly ContainerInfo? OutputContainer = outputContainer;
    public readonly NetEntity? OutputContainerEntity = outputContainerEntity;
    public readonly List<EnergyReagentInventoryItem> Inventory = inventory;
    public readonly EnergyReagentDispenserDispenseAmount SelectedAmount = amount;
    public readonly float Charge = charge;
    public readonly float MaxCharge = maxCharge;
    public readonly float Receiving = receiving;
    public readonly float IdleUse = idleUse;
    public readonly bool UsingBattery = usingBattery;
    public readonly bool HasPower = hasPower;
}

[Serializable, NetSerializable]
public enum EnergyReagentDispenserUiKey { Key }
