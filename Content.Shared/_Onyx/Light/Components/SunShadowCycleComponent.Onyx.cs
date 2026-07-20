namespace Content.Shared.Light.Components;

public sealed partial class SunShadowCycleComponent
{
    [DataField, AutoNetworkedField]
    public float PathRotation;

    [DataField, AutoNetworkedField]
    public bool Reverse;

    [DataField, AutoNetworkedField]
    public float LengthMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public float AlphaMultiplier = 1f;
}
