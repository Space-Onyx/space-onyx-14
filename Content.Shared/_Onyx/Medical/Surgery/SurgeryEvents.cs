using Content.Shared.DoAfter;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Medical.Surgery;

[Serializable, NetSerializable]
public sealed partial class SurgeryDoAfterEvent : SimpleDoAfterEvent
{
    public readonly NetEntity Part;
    public readonly EntProtoId Surgery;
    public readonly EntProtoId Step;
    public readonly uint Token;

    public SurgeryDoAfterEvent(NetEntity part, EntProtoId surgery, EntProtoId step, uint token)
    {
        Part = part;
        Surgery = surgery;
        Step = step;
        Token = token;
    }
}

public enum StepInvalidReason
{
    None,
    OutOfRange,
    NeedsOperatingTable,
    Clothing,
    MissingTool,
    IncompatibleTransplant,
    AmputationConsequence,
    IncompatibleTransplantType,
}

[ByRefEvent] public record struct SurgeryValidEvent(EntityUid Body, EntityUid Part, bool Cancelled = false);
[ByRefEvent] public record struct SurgeryStepEvent(EntityUid User, EntityUid Body, EntityUid Part, List<EntityUid> Tools);
[ByRefEvent] public record struct SurgeryStepCompleteCheckEvent(EntityUid Body, EntityUid Part, bool Cancelled = false);
[ByRefEvent] public record struct SurgeryOrganInsertedEvent(EntityUid User, EntityUid Body, EntityUid Part);
[ByRefEvent] public record struct SurgeryGetStepSequenceContextEvent(EntityUid Body, EntityUid Part, List<EntityUid> Tools, EntityUid? Context = null);

[ByRefEvent]
public record struct SurgeryCanPerformStepEvent(
    EntityUid User,
    EntityUid Body,
    EntityUid Part,
    List<EntityUid> Tools,
    SlotFlags TargetSlots,
    string? Popup = null,
    StepInvalidReason Invalid = StepInvalidReason.None,
    HashSet<EntityUid>? ValidTools = null
) : IInventoryRelayEvent;
