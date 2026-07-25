using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Xenobiology.Bounties;

[Prototype]
public sealed partial class XenobiologyBountyPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public int PointsAwarded;

    [DataField(required: true)]
    public List<XenobiologyBountyItemEntry> Entries = new();

    [DataField]
    public string IdPrefix = "NT";
}

[DataDefinition, Serializable, NetSerializable]
public readonly partial record struct XenobiologyBountyItemEntry
{
    [DataField(required: true)]
    public EntityWhitelist Whitelist { get; init; } = default!;

    [DataField]
    public EntityWhitelist? Blacklist { get; init; }

    [DataField]
    public int Amount { get; init; } = 1;

    [DataField]
    public LocId Name { get; init; } = string.Empty;
}
