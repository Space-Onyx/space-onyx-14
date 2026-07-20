namespace Content.Shared._Onyx.Bonfire.Components;

[RegisterComponent]
public sealed partial class BonfireComponent : Component
{
    [DataField]
    public float HeatPerSecond = 500f;
}
