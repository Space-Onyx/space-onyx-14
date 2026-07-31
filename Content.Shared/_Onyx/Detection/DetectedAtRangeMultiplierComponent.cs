namespace Content.Shared._Onyx.Detection;

[RegisterComponent]
public sealed partial class DetectedAtRangeMultiplierComponent : Component
{
    [DataField]
    public float ThermalMultiplier = 1f;

    [DataField]
    public float VisualMultiplier = 1f;

    [DataField]
    public float VisualBias;
}
