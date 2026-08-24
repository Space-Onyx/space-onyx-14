using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Medical.Surgery;

[RegisterComponent] public sealed partial class SurgeryOperatingTableConditionComponent : Component;
[RegisterComponent] public sealed partial class OperatingTableComponent : Component;

[RegisterComponent]
public sealed partial class SurgeryPartConditionComponent : Component
{
    [DataField] public BodyPartType? Part;
    [DataField] public HashSet<BodyPartType> Parts = new();
    [DataField] public BodyPartSymmetry? Symmetry;
    [DataField] public bool Inverse;
}

[RegisterComponent]
public sealed partial class SurgeryMarkerConditionComponent : Component
{
    [DataField] public HashSet<string> All = new();
    [DataField] public HashSet<string> Any = new();
    [DataField] public HashSet<string> None = new();
    [DataField] public HashSet<string> MissingAny = new();
}

[RegisterComponent]
public sealed partial class SurgerySpeciesConditionComponent : Component
{
    [DataField(required: true)]
    public HashSet<ProtoId<SpeciesPrototype>> Species = new();

    [DataField]
    public bool Inverse;
}

/// <summary>
/// Gates surgery on an organ tag. Without inversion requires the operated part to contain an
/// organ with the tag (removal). With inversion requires the organ being implanted to have the
/// tag (insertion, placed on the insert step).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryOrganTagConditionComponent : Component
{
    [DataField(required: true)]
    public ProtoId<TagPrototype> Tag;

    [DataField]
    public bool Inverse;
}

[RegisterComponent]
public sealed partial class SurgeryOrganConditionComponent : Component
{
    [DataField] public ProtoId<OrganCategoryPrototype>? Slot;
    [DataField] public ComponentRegistry Required = new();
    [DataField] public bool Inverse;
    [DataField] public bool Damaged;
    [DataField] public BodyPartType? Part;
}

[RegisterComponent]
public sealed partial class SurgeryMissingPartConditionComponent : Component
{
    [DataField(required: true)] public BodyPartType Part;
    [DataField] public BodyPartSymmetry Symmetry;
}

[RegisterComponent] public sealed partial class SurgeryDetachablePartConditionComponent : Component;

/// <summary>Checks component presence on the patient or operated body part.</summary>
[RegisterComponent]
public sealed partial class SurgeryComponentConditionComponent : Component
{
    [DataField]
    public SurgeryEntityTarget Target;

    [DataField]
    public ComponentRegistry All = new();

    [DataField]
    public ComponentRegistry None = new();
}

[RegisterComponent]
public sealed partial class SurgeryCavityConditionComponent : Component
{
    [DataField] public bool Occupied;
}

[RegisterComponent]
public sealed partial class SurgeryMutingConditionComponent : Component
{
    [DataField] public bool Muted;
}
