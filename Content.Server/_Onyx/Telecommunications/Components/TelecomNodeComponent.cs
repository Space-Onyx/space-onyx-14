namespace Content.Server._Onyx.Telecommunications.Components;

/// <summary>
/// Processing load and bandwidth of a telecommunications processor.
/// </summary>
[RegisterComponent]
public sealed partial class TelecomNodeComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Bandwidth = 10f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float BasePacketCost = 1f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float CharactersPerCostUnit = 80f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float LoadRecoveryPerSecond = 10f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float TelemetryRecoveryPerSecond = 1f;

    [ViewVariables(VVAccess.ReadOnly)]
    public float CurrentLoad;

    [ViewVariables(VVAccess.ReadOnly)]
    public float TelemetryLoad;
}
