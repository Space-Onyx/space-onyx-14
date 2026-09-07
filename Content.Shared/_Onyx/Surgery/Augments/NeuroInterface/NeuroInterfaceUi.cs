using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Surgery.Augments.NeuroInterface;

[Serializable, NetSerializable]
public enum NeuroInterfaceUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum NeuroConsumerStatus : byte
{
    Disabled,
    Full,
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
public sealed class NeuroInterfaceSetEnabledMessage(NetEntity augment, bool enabled) : BoundUserInterfaceMessage
{
    public NetEntity Augment = augment;
    public bool Enabled = enabled;
}

[Serializable, NetSerializable]
public sealed class NeuroInterfaceBuiState(
    List<string> modules,
    List<NeuroInterfaceBatteryData> batteries,
    List<string> powerSources,
    float powerGeneration,
    float powerConsumption,
    List<NeuroInterfaceEntryData> entries) : BoundUserInterfaceState
{
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
    NetEntity? parent,
    string name,
    string description,
    float power,
    bool enabled,
    NeuroConsumerStatus status,
    NeuroInterfaceBodyRegion region,
    bool canToggle,
    List<NeuroInterfaceTooltipSectionData> tooltipSections)
{
    public NetEntity Entity = entity;
    public NetEntity? Parent = parent;
    public string Name = name;
    public string Description = description;
    public float Power = power;
    public bool Enabled = enabled;
    public NeuroConsumerStatus Status = status;
    public NeuroInterfaceBodyRegion Region = region;
    public bool CanToggle = canToggle;
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
