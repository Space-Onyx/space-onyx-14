using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Clothing;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), Access(typeof(ClothingDirtSystem))]
public sealed partial class ClothingDirtableComponent : Component
{
    [DataField]
    public string Solution = ClothingDirtSystem.DefaultSolutionName;

    [DataField]
    public FixedPoint2 Capacity = FixedPoint2.New(20);

    [DataField]
    public FixedPoint2 MaxReagentAmount = FixedPoint2.New(10);

    [DataField]
    public float MinVisualCoverage = 0.08f;

    [AutoNetworkedField]
    public Color? DirtColor;

    [DataField]
    public float DryInterval = 30f;

    public float DryAccumulator;
}
