namespace Content.Shared._Onyx.Detection;

[RegisterComponent]
public sealed partial class HeatProducerComponent : Component
{
    [DataField]
    public float HeatPerSecond;

    [DataField]
    public bool Enabled = true;
}
