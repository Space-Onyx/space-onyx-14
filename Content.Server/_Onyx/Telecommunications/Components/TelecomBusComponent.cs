namespace Content.Server._Onyx.Telecommunications.Components;

/// <summary>
/// Marks a passive physical bus carrying telecommunications signals between adjacent nodes.
/// </summary>
[RegisterComponent]
public sealed partial class TelecomBusComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxReceivers = 2;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxProcessors = 2;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxServers = 2;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float RouteLossExponent = 2f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float RouteLossChanceMultiplier = 0.65f;
}
