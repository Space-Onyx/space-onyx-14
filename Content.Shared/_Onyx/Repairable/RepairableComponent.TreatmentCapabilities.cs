using Content.Shared.Tools;
using Content.Shared._Onyx.Wounds;
using Robust.Shared.Prototypes;

namespace Content.Shared.Repairable;

public sealed partial class RepairableComponent
{
    [DataField, AutoNetworkedField]
    public HashSet<TreatmentCapability> TreatmentCapabilities = [TreatmentCapability.Mechanical];
}

[RegisterComponent]
public sealed partial class RepairableBodyPartComponent : Component
{
    [DataField]
    public float FuelCost = 5f;

    [DataField]
    public ProtoId<ToolQualityPrototype> QualityNeeded = "Welding";

    [DataField]
    public bool AutoDoAfter = true;

    [DataField]
    public float SelfRepairPenalty = 3f;

    [DataField]
    public bool AllowSelfRepair = true;

    [DataField]
    public HashSet<TreatmentCapability> TreatmentCapabilities = [TreatmentCapability.Mechanical];
}
