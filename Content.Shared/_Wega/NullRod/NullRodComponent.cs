using Content.Shared.FixedPoint;

namespace Content.Shared.NullRod.Components;

[RegisterComponent]
public sealed partial class NullRodComponent : Component
{
    [DataField]
    public FixedPoint2 FirstNullDamage = 30;

    [DataField]
    public FixedPoint2 NullDamage = 15;
}
