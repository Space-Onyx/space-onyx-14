using Content.Shared.EntityConditions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Salvage.Pressure;

public sealed partial class PressureThreshold : EntityConditionBase<PressureThreshold>
{
    [DataField]
    public bool WorksOnLavaland;

    [DataField]
    public float Min = float.MinValue;

    [DataField]
    public float Max = float.MaxValue;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype) =>
        Loc.GetString("reagent-effect-condition-pressure-threshold", ("min", Min), ("max", Max));
}
