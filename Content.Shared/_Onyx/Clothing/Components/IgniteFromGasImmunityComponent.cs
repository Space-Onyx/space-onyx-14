using Content.Shared._Onyx.Targeting;

namespace Content.Shared._Onyx.Clothing.Components;

[RegisterComponent]
public sealed partial class IgniteFromGasImmunityComponent : Component
{
    [DataField(required: true)]
    public HashSet<TargetBodyPart> Parts = new();
}
