using Content.Shared.Stacks;
using Content.Shared.Tools;
using Content.Shared.Body.Part;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.Medical.Surgery;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedSurgerySystem))]
public sealed partial class SurgeryTargetComponent : Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedSurgerySystem))]
public sealed partial class SurgeryComponent : Component
{
    [DataField, AutoNetworkedField] public int Priority;
    [DataField, AutoNetworkedField] public EntProtoId? Requirement;
    [DataField, AutoNetworkedField] public SpriteSpecifier? Icon;
    [DataField, AutoNetworkedField] public bool UseTargetPartIcon;
    [DataField(required: true)] public Dictionary<string, SurgeryStepSequence> Steps = new();
}

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedSurgerySystem))]
public sealed partial class SurgeryStepComponent : Component
{
    [DataField] public float Duration = 2f;
    [DataField] public ComponentRegistry? Tool;
    [DataField] public ProtoId<ToolQualityPrototype>? ToolQuality;
    [DataField] public ProtoId<StackPrototype>? ConsumedStackType;
    [DataField] public EntProtoId? ConsumedPrototype;
    [DataField] public int ConsumedAmount = 1;
    [DataField] public HashSet<string> AddMarkers = new();
    [DataField] public HashSet<string> RemoveMarkers = new();
    [DataField] public HashSet<string> ParentRemoveMarkers = new();
    [DataField] public BodyPartType? ParentRemoveMarkersPart;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgeryMarkerComponent : Component
{
    [DataField, AutoNetworkedField] public HashSet<string> Markers = new();
}

[RegisterComponent] public sealed partial class MechanicalSurgeryStepComponent : Component;

[RegisterComponent] public sealed partial class MechanicalOrganComponent : Component;

[RegisterComponent] public sealed partial class SlimeCoreComponent : Component;

[RegisterComponent] public sealed partial class TorsoOrganComponent : Component;
