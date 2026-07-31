using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Shuttles.Events;

[Serializable, NetSerializable]
public enum InertiaDampeningMode : byte
{
    None,
    Cruise,
    Dampen,
    Anchor,
}

[Serializable, NetSerializable]
public sealed class SetInertiaDampeningRequest : BoundUserInterfaceMessage
{
    public InertiaDampeningMode Mode;
}
