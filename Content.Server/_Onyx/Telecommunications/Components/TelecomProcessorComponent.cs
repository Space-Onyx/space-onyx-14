namespace Content.Server._Onyx.Telecommunications.Components;

/// <summary>
/// Processes radio traffic accepted by a telecommunications receiver.
/// </summary>
[RegisterComponent]
public sealed partial class TelecomProcessorComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxBuses = 2;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float GarbleQualityThreshold = 0.95f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float GarbleChanceMultiplier = 0.35f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float CalibrationLatencyMultiplier = 800f;
}
