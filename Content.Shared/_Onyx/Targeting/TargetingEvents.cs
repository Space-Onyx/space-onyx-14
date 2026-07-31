using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Targeting;

[Serializable, NetSerializable]
public sealed class TargetChangeRequest(TargetBodyPart requestedPart) : EntityEventArgs
{
    public readonly TargetBodyPart RequestedPart = requestedPart;
}

[Serializable, NetSerializable]
public sealed class PartStatusExamineRequest : EntityEventArgs;
