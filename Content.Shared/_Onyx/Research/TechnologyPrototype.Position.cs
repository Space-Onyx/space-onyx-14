using Robust.Shared.Maths;

namespace Content.Shared.Research.Prototypes;

public sealed partial class TechnologyPrototype
{
    [DataField(required: true)]
    public Vector2i Position { get; private set; }
}
