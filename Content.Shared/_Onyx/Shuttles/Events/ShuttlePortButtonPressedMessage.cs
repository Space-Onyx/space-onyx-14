using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Shuttles.Events;

[Serializable, NetSerializable]
public sealed class ShuttlePortButtonPressedMessage : BoundUserInterfaceMessage
{
    public string SourcePort { get; set; } = string.Empty;
}
