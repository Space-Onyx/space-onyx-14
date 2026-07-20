namespace Content.Server._Onyx.Telecommunications.Components;

/// <summary>
/// Receives radio traffic before it enters the telecommunications processing chain.
/// </summary>
[RegisterComponent]
public sealed partial class TelecomReceiverComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxBuses = 2;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float LossExponent = 2f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float LossChanceMultiplier = 0.65f;
}
