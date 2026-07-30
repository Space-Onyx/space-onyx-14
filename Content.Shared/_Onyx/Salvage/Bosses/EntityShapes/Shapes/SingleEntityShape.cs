using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Salvage.Bosses.EntityShapes.Shapes;

/// <summary>
/// Returns a singe tile at the specified position.
/// </summary>
public sealed partial class SingleEntityShape : EntityShape
{
    protected override List<Vector2> GetShapeImplementation(System.Random rand, IPrototypeManager proto)
    {
        return new List<Vector2> { Offset };
    }
}
