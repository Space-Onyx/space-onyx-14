namespace Content.Server._Onyx.Telecommunications.Components;

/// <summary>
/// Tracks the alignment of a telecommunications receiver or processor.
/// </summary>
[RegisterComponent]
public sealed partial class TelecomCalibrationComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Calibration = 100f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float DecayPerHour = 12f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float DamageLossMultiplier = 0.15f;
}
