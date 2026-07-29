namespace Content.Client._Onyx.Power.PTL;

[RegisterComponent]
public sealed partial class PTLVisualsComponent : Component
{
    [DataField] public string ChargePrefix = "charge-";
    [DataField] public int MaxChargeStates = 6;
}
