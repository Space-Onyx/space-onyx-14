using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Research;

/// <summary>
/// A typed amount of research points, used for balances, rewards and technology costs.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public partial record struct ResearchPointAmount
{
    public const string General = "General";

    [DataField(required: true)]
    public ProtoId<Prototypes.ResearchPointTypePrototype> Type;

    [DataField]
    public int Amount;

    public ResearchPointAmount(string type, int amount)
    {
        Type = type;
        Amount = amount;
    }
}

[Serializable, NetSerializable]
public enum ResearchNetworkLogType : byte
{
    ServerOnline,
    ServerOffline,
    GenerationToggled,
    PointsChanged,
    TechnologyUnlocked,
    TechnologyRevealed,
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
