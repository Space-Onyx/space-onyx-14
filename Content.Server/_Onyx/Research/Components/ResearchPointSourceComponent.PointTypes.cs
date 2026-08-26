using Content.Shared._Onyx.Research;

namespace Content.Server.Research.Components;

public sealed partial class ResearchPointSourceComponent
{
    /// <summary>
    /// The point type credited by this source. Non-General types bypass the legacy integer channel.
    /// </summary>
    [DataField("pointType"), ViewVariables(VVAccess.ReadWrite)]
    public string PointType = ResearchPointAmount.General;
}
