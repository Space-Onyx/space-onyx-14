using Content.Shared.DeviceNetwork;
using Robust.Shared.Serialization;

namespace Content.Shared.Robotics;

[Serializable, NetSerializable]
public sealed class RoboticsConsoleChangeLawsMessage(DeviceAddress address) : BoundUserInterfaceMessage
{
    public readonly DeviceAddress Address = address;
}
