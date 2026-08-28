using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Metabolism;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Chemistry.Circulation;

[Prototype]
public sealed partial class CirculatoryStreamPrototype : IPrototype
{
    public static readonly ProtoId<CirculatoryStreamPrototype> PrimaryStream = "Organic";

    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string SolutionName = "bloodstream";

    [DataField]
    public string MetabolitesSolutionName = "metabolites";

    [DataField]
    public string TemporarySolutionName = "bloodstreamTemporary";

    [DataField]
    public ProtoId<MetabolismStagePrototype> MetabolismStage = "Bloodstream";

    [DataField]
    public ProtoId<MetabolismStagePrototype> MetabolitesStage = "Metabolites";

    [DataField]
    public Solution ReferenceSolution = new(new[] { new ReagentQuantity("Blood", 600) });

    [DataField]
    public float MaxVolumeModifier = 2f;

    [DataField]
    public FixedPoint2 MetabolismTransferRate = FixedPoint2.New(0.25f);

    [DataField]
    public int MaxReagentsProcessable = 3;
}
