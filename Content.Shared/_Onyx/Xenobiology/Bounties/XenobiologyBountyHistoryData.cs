using Content.Shared.Cargo;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Xenobiology.Bounties;

[DataDefinition, NetSerializable, Serializable]
public readonly partial record struct XenobiologyBountyHistoryData
{
    [DataField]
    public string Id { get; init; }

    [DataField]
    public CargoBountyHistoryData.BountyResult Result { get; init; }

    [DataField]
    public string? ActorName { get; init; }

    [DataField]
    public TimeSpan Timestamp { get; init; }

    [DataField(required: true)]
    public ProtoId<XenobiologyBountyPrototype> Bounty { get; init; }

    public XenobiologyBountyHistoryData(
        XenobiologyBountyData bounty,
        CargoBountyHistoryData.BountyResult result,
        TimeSpan timestamp,
        string? actorName)
    {
        Bounty = bounty.Bounty;
        Result = result;
        Id = bounty.Id;
        ActorName = actorName;
        Timestamp = timestamp;
    }
}
