using Content.Shared.Body.Part;
using Content.Shared.Body;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Medical.Surgery;

[Serializable, NetSerializable]
public enum SurgeryUIKey { Key }

[Serializable, NetSerializable]
public sealed class SurgeryBuiState(Dictionary<NetEntity, List<EntProtoId>> choices) : BoundUserInterfaceState
{
    public readonly Dictionary<NetEntity, List<EntProtoId>> Choices = choices;
}

[Serializable, NetSerializable]
public sealed class SurgeryStepChosenBuiMsg(NetEntity part, EntProtoId surgery, EntProtoId step) : BoundUserInterfaceMessage
{
    public readonly NetEntity Part = part;
    public readonly EntProtoId Surgery = surgery;
    public readonly EntProtoId Step = step;
}

[Serializable, NetSerializable]
public sealed class SurgeryStepsStateRequest(NetEntity part, EntProtoId surgery) : BoundUserInterfaceMessage
{
    public readonly NetEntity Part = part;
    public readonly EntProtoId Surgery = surgery;
}

[Serializable, NetSerializable]
public sealed class SurgeryStepsStateResponse(
    NetEntity part,
    EntProtoId surgery,
    List<bool> completed,
    int nextStep,
    bool available,
    string? popup,
    StepInvalidReason reason) : BoundUserInterfaceMessage
{
    public readonly NetEntity Part = part;
    public readonly EntProtoId Surgery = surgery;
    public readonly List<bool> Completed = completed;
    public readonly int NextStep = nextStep;
    public readonly bool Available = available;
    public readonly string? Popup = popup;
    public readonly StepInvalidReason Reason = reason;
}

[Serializable, NetSerializable]
public sealed partial class SurgeryDoAfterEvent : SimpleDoAfterEvent
{
    public readonly NetEntity Part;
    public readonly EntProtoId Surgery;
    public readonly EntProtoId Step;

    public SurgeryDoAfterEvent(NetEntity part, EntProtoId surgery, EntProtoId step)
    {
        Part = part;
        Surgery = surgery;
        Step = step;
    }
}

public enum StepInvalidReason { None, OutOfRange, NeedsOperatingTable, Clothing, MissingTool }

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedSurgerySystem))]
public sealed partial class SurgeryTargetComponent : Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedSurgerySystem))]
public sealed partial class SurgeryComponent : Component
{
    [DataField, AutoNetworkedField] public int Priority;
    [DataField, AutoNetworkedField] public EntProtoId? Requirement;
    [DataField(required: true), AutoNetworkedField] public List<EntProtoId> Steps = new();
}

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedSurgerySystem))]
public sealed partial class SurgeryStepComponent : Component
{
    [DataField] public float Duration = 2f;
    [DataField] public ComponentRegistry? Tool;
    [DataField] public ComponentRegistry? Add;
    [DataField] public ComponentRegistry? Remove;
    [DataField] public ComponentRegistry? BodyRemove;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedSurgerySystem), typeof(SurgeryToolExamineSystem))]
public sealed partial class SurgeryToolComponent : Component
{
    [DataField, AutoNetworkedField] public Dictionary<string, float> SpeedModifiers = new();
    [DataField, AutoNetworkedField] public List<LocId> CustomUses = new();
    [DataField, AutoNetworkedField] public SoundSpecifier? StartSound;
    [DataField, AutoNetworkedField] public SoundSpecifier? EndSound;
}

[RegisterComponent] public sealed partial class ScalpelComponent : Component;
[RegisterComponent] public sealed partial class HemostatComponent : Component;
[RegisterComponent] public sealed partial class RetractorComponent : Component;
[RegisterComponent] public sealed partial class BoneSawComponent : Component;
[RegisterComponent] public sealed partial class CauteryComponent : Component;
[RegisterComponent] public sealed partial class BoneGelComponent : Component;
[RegisterComponent] public sealed partial class TweezersComponent : Component;
[RegisterComponent] public sealed partial class DrillComponent : Component;
[RegisterComponent] public sealed partial class StitchesComponent : Component;
[RegisterComponent] public sealed partial class BoneSetterComponent : Component;
[RegisterComponent] public sealed partial class TendingComponent : Component;

[RegisterComponent, NetworkedComponent] public sealed partial class IncisionOpenComponent : Component;
[RegisterComponent, NetworkedComponent] public sealed partial class BleedersClampedComponent : Component;
[RegisterComponent, NetworkedComponent] public sealed partial class SkinRetractedComponent : Component;
[RegisterComponent, NetworkedComponent] public sealed partial class RibcageSawedComponent : Component;
[RegisterComponent, NetworkedComponent] public sealed partial class RibcageOpenComponent : Component;
[RegisterComponent, NetworkedComponent] public sealed partial class AbdominalCavityOpenComponent : Component;
[RegisterComponent, NetworkedComponent] public sealed partial class BodyPartReattachedComponent : Component;
[RegisterComponent, NetworkedComponent] public sealed partial class BodyPartMendedComponent : Component;
[RegisterComponent, NetworkedComponent] public sealed partial class BodyPartSuturedComponent : Component;

[RegisterComponent] public sealed partial class SurgeryOperatingTableConditionComponent : Component;
[RegisterComponent] public sealed partial class OperatingTableComponent : Component;

[RegisterComponent]
public sealed partial class SurgeryPartConditionComponent : Component
{
    [DataField(required: true)] public BodyPartType Part;
    [DataField] public BodyPartSymmetry? Symmetry;
}

[RegisterComponent] public sealed partial class SurgeryCloseIncisionConditionComponent : Component;

[RegisterComponent]
public sealed partial class SurgeryStepBleedEffectComponent : Component
{
    [DataField] public int Damage;
}

[RegisterComponent] public sealed partial class SurgeryClampBleedEffectComponent : Component;

[RegisterComponent] public sealed partial class SurgeryCloseIncisionEffectComponent : Component;

[RegisterComponent] public sealed partial class SurgeryDetachPartEffectComponent : Component;

[RegisterComponent]
public sealed partial class SurgeryAttachPartEffectComponent : Component
{
    [DataField(required: true)] public BodyPartType Part;
    [DataField] public BodyPartSymmetry Symmetry;
}

[RegisterComponent]
public sealed partial class SurgeryMendAttachedPartEffectComponent : Component
{
    [DataField(required: true)] public BodyPartType Part;
    [DataField] public BodyPartSymmetry Symmetry;
}

[RegisterComponent]
public sealed partial class SurgerySutureAttachedPartEffectComponent : Component
{
    [DataField(required: true)] public BodyPartType Part;
    [DataField] public BodyPartSymmetry Symmetry;
}

[RegisterComponent]
public sealed partial class SurgeryOrganConditionComponent : Component
{
    [DataField(required: true)] public ProtoId<OrganCategoryPrototype> Slot;
    [DataField] public bool Inverse;
    [DataField] public bool Damaged;
    [DataField] public BodyPartType? Part;
}

[RegisterComponent]
public sealed partial class SurgeryOrganHealEffectComponent : Component
{
    [DataField(required: true)] public ProtoId<OrganCategoryPrototype> Slot;
    [DataField(required: true)] public FixedPoint2 Amount;
}

[RegisterComponent]
public sealed partial class SurgeryRemoveOrganEffectComponent : Component
{
    [DataField(required: true)] public ProtoId<OrganCategoryPrototype> Slot;
}

[RegisterComponent]
public sealed partial class SurgeryInsertOrganEffectComponent : Component
{
    [DataField(required: true)] public ProtoId<OrganCategoryPrototype> Slot;
}

[RegisterComponent]
public sealed partial class SurgeryMissingPartConditionComponent : Component
{
    [DataField(required: true)] public BodyPartType Part;
    [DataField] public BodyPartSymmetry Symmetry;
}

[RegisterComponent] public sealed partial class SurgeryDetachablePartConditionComponent : Component;

[RegisterComponent]
public sealed partial class SurgeryCavityConditionComponent : Component
{
    [DataField] public bool Occupied;
}

[RegisterComponent] public sealed partial class SurgeryInsertCavityItemEffectComponent : Component;
[RegisterComponent] public sealed partial class SurgeryRemoveCavityItemEffectComponent : Component;

[RegisterComponent]
public sealed partial class SurgeryStepEmoteEffectComponent : Component
{
    [DataField] public string Emote = "Scream";
}

[ByRefEvent] public record struct SurgeryValidEvent(EntityUid Body, EntityUid Part, bool Cancelled = false);
[ByRefEvent] public record struct SurgeryStepEvent(EntityUid User, EntityUid Body, EntityUid Part, List<EntityUid> Tools);
[ByRefEvent] public record struct SurgeryStepCompleteCheckEvent(EntityUid Body, EntityUid Part, bool Cancelled = false);

[ByRefEvent]
public record struct SurgeryCanPerformStepEvent(
    EntityUid User,
    EntityUid Body,
    List<EntityUid> Tools,
    SlotFlags TargetSlots,
    string? Popup = null,
    StepInvalidReason Invalid = StepInvalidReason.None,
    HashSet<EntityUid>? ValidTools = null
) : IInventoryRelayEvent;
