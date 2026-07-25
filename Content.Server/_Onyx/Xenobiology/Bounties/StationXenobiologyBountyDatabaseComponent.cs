using Content.Shared._Onyx.Xenobiology.Bounties;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Onyx.Xenobiology.Bounties;

[RegisterComponent]
public sealed partial class StationXenobiologyBountyDatabaseComponent : Component
{
    [DataField]
    public int MaxBounties = 27;

    [DataField]
    public List<XenobiologyBountyData> Bounties = new();

    [DataField]
    public List<XenobiologyBountyHistoryData> History = new();

    [DataField]
    public int NextIdentifier;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextSkipTime;

    [DataField]
    public TimeSpan SkipDelay = TimeSpan.FromMinutes(10);

    [DataField]
    public TimeSpan RefreshDelay = TimeSpan.FromMinutes(10);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextRefreshTime;
}
