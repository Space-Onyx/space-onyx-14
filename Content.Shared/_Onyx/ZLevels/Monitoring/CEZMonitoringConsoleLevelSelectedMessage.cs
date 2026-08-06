using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.ZLevels.Monitoring;

[Serializable, NetSerializable]
public sealed class CEZMonitoringConsoleLevelSelectedMessage : BoundUserInterfaceMessage
{
    public NetEntity? Grid { get; init; }
    public int Depth { get; init; }

    public CEZMonitoringConsoleLevelSelectedMessage(NetEntity? grid, int depth)
    {
        Grid = grid;
        Depth = depth;
    }
}
