using System.Numerics;

namespace Content.Server._Onyx.Radar;

[RegisterComponent]
public sealed partial class HitscanRadarComponent : Component
{
    [DataField] public Vector2 Start;
    [DataField] public Vector2 End;
    [DataField] public float Thickness = 1f;
    [DataField] public Color Color = Color.Magenta;
}
