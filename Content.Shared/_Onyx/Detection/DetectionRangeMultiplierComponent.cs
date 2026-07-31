namespace Content.Shared._Onyx.Detection;

[RegisterComponent]
public sealed partial class DetectionRangeMultiplierComponent : Component
{
    [DataField]
    public float ThermalMultiplier = 1f;

    [DataField]
    public float ThermalOutlinePortion = 0.6f;

    [DataField]
    public float VisualMultiplier = 1f;

    [DataField]
    public bool AlwaysDetect;
}
