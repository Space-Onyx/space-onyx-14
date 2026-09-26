namespace Content.Shared._Onyx.Telecommunications;

/// <summary>
/// Allows powered machines to relay headset radio messages between maps.
/// </summary>
[RegisterComponent]
public sealed partial class TelecomTransmitterComponent : Component
{
    /// <summary>
    /// Shapes signal loss as transmitter condition falls.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float LossExponent = 2f;

    /// <summary>
    /// Scales signal loss caused by transmitter wear.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float LossChanceMultiplier = 0.65f;

    /// <summary>
    /// Utilization above which congestion starts dropping signals.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float CongestionDropThreshold = 1f;

    /// <summary>
    /// Scales congestion loss above the threshold.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float CongestionDropChanceMultiplier = 0.4f;

    /// <summary>
    /// Maximum congestion loss chance per long-range hop.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxCongestionDropChance = 0.75f;
}
