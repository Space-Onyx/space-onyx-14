using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Medical.Surgery;

[Serializable, NetSerializable]
public enum SurgeryUIKey { Key }

[Serializable, NetSerializable]
public enum SurgerySelectionState : byte
{
    Active,
    Completed,
    Invalid,
}

[Serializable, NetSerializable]
public sealed class SurgeryBuiState(
    Dictionary<NetEntity, List<EntProtoId>> choices,
    Dictionary<NetEntity, HashSet<EntProtoId>> completed) : BoundUserInterfaceState
{
    public readonly Dictionary<NetEntity, List<EntProtoId>> Choices = choices;
    public readonly Dictionary<NetEntity, HashSet<EntProtoId>> Completed = completed;
}

[Serializable, NetSerializable]
public sealed class SurgeryStepChosenBuiMsg(NetEntity part, EntProtoId surgery, EntProtoId step) : BoundUserInterfaceMessage
{
    public readonly NetEntity Part = part;
    public readonly EntProtoId Surgery = surgery;
    public readonly EntProtoId Step = step;
}

[Serializable, NetSerializable]
public sealed class SurgeryStepsStateRequest(NetEntity part, EntProtoId surgery, uint requestId) : BoundUserInterfaceMessage
{
    public readonly NetEntity Part = part;
    public readonly EntProtoId Surgery = surgery;
    public readonly uint RequestId = requestId;
}

[Serializable, NetSerializable]
public sealed class SurgeryStepsStateResponse(
    NetEntity part,
    EntProtoId surgery,
    List<EntProtoId> steps,
    List<bool> completed,
    int nextStep,
    bool available,
    string? popup,
    StepInvalidReason reason,
    uint requestId,
    SurgerySelectionState selectionState) : BoundUserInterfaceMessage
{
    public readonly NetEntity Part = part;
    public readonly EntProtoId Surgery = surgery;
    public readonly List<EntProtoId> Steps = steps;
    public readonly List<bool> Completed = completed;
    public readonly int NextStep = nextStep;
    public readonly bool Available = available;
    public readonly string? Popup = popup;
    public readonly StepInvalidReason Reason = reason;
    public readonly uint RequestId = requestId;
    public readonly SurgerySelectionState SelectionState = selectionState;
}
