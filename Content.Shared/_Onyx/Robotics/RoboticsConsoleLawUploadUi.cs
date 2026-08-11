using Robust.Shared.Serialization;

namespace Content.Shared.Robotics;

[Serializable, NetSerializable]
public sealed class RoboticsConsoleChangeLawsMessage(string address) : BoundUserInterfaceMessage
{
    public readonly string Address = address;
}
