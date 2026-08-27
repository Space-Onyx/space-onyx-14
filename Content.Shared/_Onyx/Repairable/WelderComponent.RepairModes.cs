using Content.Shared.Damage;
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
    public DamageSpecifier Damage = new();

    [DataField]
    public int DoAfterDelay = 1;

    [DataField]
    public HashSet<TreatmentCapability> TreatmentCapabilities = [TreatmentCapability.Mechanical];

    [DataField]
    public float FuelMultiplier = 1f;
}
