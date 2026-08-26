using Content.Shared._Onyx.Research;

namespace Content.Server.Research.Disk;

public sealed partial class ResearchDiskComponent
{
    [DataField]
    public bool GrantExperimentalPoints;

    /// <summary>
    /// Typed point rewards granted by this disk. When set, overrides <see cref="Points"/>.
    /// </summary>
    [DataField]
    public List<ResearchPointAmount>? PointRewards;
}
