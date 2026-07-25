using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Xenobiology.Bounties;

[DataDefinition, NetSerializable, Serializable]
public readonly partial record struct XenobiologyBountyData
{
    [DataField]
    public string Id { get; init; }

    [DataField(required: true)]
    public ProtoId<XenobiologyBountyPrototype> Bounty { get; init; }

    public XenobiologyBountyData(XenobiologyBountyPrototype bounty, int identifier)
    {
        Bounty = bounty.ID;
        Id = $"{bounty.IdPrefix}{identifier:D4}";
    }
}
