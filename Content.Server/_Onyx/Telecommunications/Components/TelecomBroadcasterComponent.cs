namespace Content.Server._Onyx.Telecommunications.Components;

/// <summary>
/// Broadcasts radio traffic that has passed through the telecommunications chain.
/// </summary>
[RegisterComponent]
public sealed partial class TelecomBroadcasterComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxServers = 2;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Condition = 100f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float DamageLossMultiplier = 0.15f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float OutputLossExponent = 2f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float OutputLossChanceMultiplier = 0.65f;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool Sabotaged;
}
