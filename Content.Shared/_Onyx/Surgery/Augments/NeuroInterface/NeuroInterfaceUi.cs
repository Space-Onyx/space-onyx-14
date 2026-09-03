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
    Emp,
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
public enum NeuroRoutingAction : byte
{
    Add,
    Remove,
    MoveUp,
    MoveDown,
}

[Serializable, NetSerializable]
public sealed class NeuroInterfaceSetRoutingMessage(NetEntity augment, NeuroRoutingAction action) : BoundUserInterfaceMessage
{
    public NetEntity Augment = augment;
    public NeuroRoutingAction Action = action;
}

[Serializable, NetSerializable]
public sealed class NeuroInterfaceBuiState(
    NeuroInterfaceMode mode,
    float bandwidth,
    float demand,
    float overload,
    int channelOverload,
    int channels,
    int channelCapacity,
    string? chipName,
    string? cacheName,
    string? routerName,
    float chipBandwidth,
    int chipChannels,
    int cacheChannels,
    int routerCapacity,
    int routedCount,
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
    public int ChannelOverload = channelOverload;
    public int Channels = channels;
    public int ChannelCapacity = channelCapacity;
    public string? ChipName = chipName;
    public string? CacheName = cacheName;
    public string? RouterName = routerName;
    public float ChipBandwidth = chipBandwidth;
    public int ChipChannels = chipChannels;
    public int CacheChannels = cacheChannels;
    public int RouterCapacity = routerCapacity;
    public int RoutedCount = routedCount;
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
    string description,
    float demand,
    float power,
    bool enabled,
    bool routed,
    int routingOrder,
    float efficiency,
    NeuroConsumerStatus status,
    bool scalable,
    NeuroInterfaceBodyRegion region,
    bool showTelemetry,
    bool canToggle,
    bool canRoute,
    List<NeuroInterfaceTooltipSectionData> tooltipSections)
{
    public NetEntity Entity = entity;
    public string Name = name;
    public string Description = description;
    public float Demand = demand;
    public float Power = power;
    public bool Enabled = enabled;
    public bool Routed = routed;
    public int RoutingOrder = routingOrder;
    public float Efficiency = efficiency;
    public NeuroConsumerStatus Status = status;
    public bool Scalable = scalable;
    public NeuroInterfaceBodyRegion Region = region;
    public bool ShowTelemetry = showTelemetry;
    public bool CanToggle = canToggle;
    public bool CanRoute = canRoute;
    public List<NeuroInterfaceTooltipSectionData> TooltipSections = tooltipSections;
}

[Serializable, NetSerializable]
public sealed class NeuroInterfaceTooltipSectionData(string id, string title, List<string> lines)
{
    public string Id = id;
    public string Title = title;
    public List<string> Lines = lines;
}

public sealed class CollectNeuroInterfaceTooltipEvent : EntityEventArgs
{
    public readonly List<NeuroInterfaceTooltipSectionData> Sections = new();

    public void AddSection(string id, string title, params string[] lines)
    {
        foreach (var section in Sections)
        {
            if (section.Id != id)
                continue;

            section.Lines.AddRange(lines);
            return;
        }

        Sections.Add(new NeuroInterfaceTooltipSectionData(id, title, new List<string>(lines)));
    }
}
