namespace Content.Server._Onyx.Telecommunications.Components;

[RegisterComponent]
public sealed partial class TelecomRouterComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxBuses = 1;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool Standalone;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float CongestionDropThreshold = 1f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float CongestionDropChanceMultiplier = 0.35f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxCongestionDropChance = 0.65f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxTotalDropChance = 0.8f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float CriticalOverloadThreshold = 1.5f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float CriticalOverloadDropChance = 0.9f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float CriticalOverloadGarbleChance = 1f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float GarbleLoadThreshold = 0.75f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float GarbleLoadChanceMultiplier = 0.12f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxGarbleChance = 0.7f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float GarbleCharacterChance = 0.45f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float GarbleAlternateSymbolChance = 0.5f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float DegradedQualityThreshold = 0.9f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float DegradedLoadThreshold = 0.75f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ChannelErrorExponent = 2f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ChannelErrorChanceMultiplier = 0.45f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float LoggingFailureExponent = 2f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float LoggingFailureChanceMultiplier = 0.35f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ChannelOutageExponent = 3f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ChannelOutageChanceMultiplier = 0.2f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ChannelOutageDurationSeconds = 20f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float BaseLatencyMilliseconds = 40f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float LoadLatencyThreshold = 0.5f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float LoadLatencyMultiplier = 1200f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public HashSet<string> DisabledChannels = new();

    [ViewVariables(VVAccess.ReadOnly)]
    public bool Sabotaged;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool ResetArmed;

    [ViewVariables]
    public readonly Dictionary<string, TimeSpan> FaultedChannels = new();
}
