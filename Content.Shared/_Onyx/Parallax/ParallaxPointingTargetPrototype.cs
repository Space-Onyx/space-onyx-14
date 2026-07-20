using Robust.Shared.Prototypes;

namespace Content.Shared.Parallax;

[Prototype]
public sealed partial class ParallaxPointingTargetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;
}
