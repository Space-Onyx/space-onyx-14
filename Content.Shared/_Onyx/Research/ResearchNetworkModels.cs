using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Research;

[Serializable, NetSerializable]
public enum ResearchNetworkLogType : byte
{
    ServerOnline,
    ServerOffline,
    GenerationToggled,
    PointsChanged,
    TechnologyUnlocked,
    NetworkChanged,
}

[DataDefinition, Serializable, NetSerializable]
public partial record struct ResearchNetworkLogEntry
{
    [DataField]
    public TimeSpan Timestamp;

    [DataField]
    public ResearchNetworkLogType Type;

    [DataField]
    public string Message;
}
