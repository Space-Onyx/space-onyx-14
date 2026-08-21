using Content.Shared.Damage.Prototypes;
using Content.Shared._Onyx.Wounds;
using Robust.Shared.GameStates;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Repairable;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(WelderRepairSystem))]
public sealed partial class WelderRepairModesComponent : Component
{
    [DataField, AutoNetworkedField]
    public string RepairMode = string.Empty;

    [DataField]
    public Dictionary<string, WelderRepairMode> RepairModes = new();
}

[DataDefinition]
public sealed partial class WelderRepairMode
{
    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public ProtoId<DamageGroupPrototype> DamageGroup;

    [DataField]
    public HashSet<TreatmentCapability> TreatmentCapabilities = [TreatmentCapability.Mechanical];

    [DataField]
    public float RepairMultiplier = 1f;

    [DataField]
    public float DelayMultiplier = 1f;

    [DataField]
    public float FuelMultiplier = 1f;
}
