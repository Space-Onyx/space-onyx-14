using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Surgery.Augments.NeuroInterface;

[Serializable, NetSerializable]
public enum NeuroInterfaceUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum NeuroInterfaceMode : byte
{
    Throttle,
    Overclock,
}

[Serializable, NetSerializable]
public enum NeuroConsumerStatus : byte
{
    Disabled,
    Offline,
    Full,
    Throttled,
}

[Serializable, NetSerializable]
public enum NeuroInterfaceBodyRegion : byte
{
    Head,
    Chest,
    Groin,
    LeftArm,
    RightArm,
    LeftHand,
    RightHand,
    LeftLeg,
    RightLeg,
    LeftFoot,
    RightFoot,
    Other,
}

[Serializable, NetSerializable]
public sealed class NeuroInterfaceSetModeMessage(NeuroInterfaceMode mode) : BoundUserInterfaceMessage
{
    public NeuroInterfaceMode Mode = mode;
}

[Serializable, NetSerializable]
public sealed class NeuroInterfaceSetEnabledMessage(NetEntity augment, bool enabled) : BoundUserInterfaceMessage
{
    public NetEntity Augment = augment;
    public bool Enabled = enabled;
}

[Serializable, NetSerializable]
public sealed class NeuroInterfaceSetPriorityMessage(NetEntity augment, int priority) : BoundUserInterfaceMessage
{
    public NetEntity Augment = augment;
    public int Priority = priority;
}

[Serializable, NetSerializable]
public sealed class NeuroInterfaceBuiState(
    NeuroInterfaceMode mode,
    float bandwidth,
    float demand,
    float overload,
    int channels,
    int channelCapacity,
    string? chipName,
    string? cacheName,
    List<string> modules,
    List<NeuroInterfaceBatteryData> batteries,
    List<string> powerSources,
    float powerGeneration,
    float powerConsumption,
    List<NeuroInterfaceEntryData> entries) : BoundUserInterfaceState
{
    public NeuroInterfaceMode Mode = mode;
    public float Bandwidth = bandwidth;
    public float Demand = demand;
    public float Overload = overload;
    public int Channels = channels;
    public int ChannelCapacity = channelCapacity;
    public string? ChipName = chipName;
    public string? CacheName = cacheName;
    public List<string> Modules = modules;
    public List<NeuroInterfaceBatteryData> Batteries = batteries;
    public List<string> PowerSources = powerSources;
    public float PowerGeneration = powerGeneration;
    public float PowerConsumption = powerConsumption;
    public List<NeuroInterfaceEntryData> Entries = entries;
}

[Serializable, NetSerializable]
public sealed class NeuroInterfaceBatteryData(string name, float charge, float capacity, float chargeRate)
{
    public string Name = name;
    public float Charge = charge;
    public float Capacity = capacity;
    public float ChargeRate = chargeRate;
}

[Serializable, NetSerializable]
public sealed class NeuroInterfaceEntryData(
    NetEntity entity,
    string name,
    float demand,
    float power,
    bool enabled,
    int priority,
    float efficiency,
    string status,
    NeuroInterfaceBodyRegion region)
{
    public NetEntity Entity = entity;
    public string Name = name;
    public float Demand = demand;
    public float Power = power;
    public bool Enabled = enabled;
    public int Priority = priority;
    public float Efficiency = efficiency;
    public string Status = status;
    public NeuroInterfaceBodyRegion Region = region;
}
