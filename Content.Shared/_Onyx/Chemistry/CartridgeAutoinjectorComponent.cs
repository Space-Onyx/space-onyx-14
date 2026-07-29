using Content.Shared.Chemistry.Components;

namespace Content.Shared.Chemistry.EntitySystems;

[RegisterComponent]
public sealed partial class CartridgeAutoinjectorComponent : Component;

[RegisterComponent]
public sealed partial class SolutionCartridgeComponent : Component
{
    [DataField]
    public string TargetSolution = "hypospray";

    [DataField(required: true)]
    public Solution Solution = default!;
}
