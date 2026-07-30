using Content.Shared._Onyx.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Timing;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Shuttles;

[Serializable, NetSerializable]
public enum DockingConsoleUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class DockingConsoleState(bool hasShuttle, FTLState ftlState, StartEndTime ftlTime, List<DockingDestination> destinations) : BoundUserInterfaceState
{
    public bool HasShuttle = hasShuttle;
    public FTLState FTLState = ftlState;
    public StartEndTime FTLTime = ftlTime;
    public List<DockingDestination> Destinations = destinations;
}

[Serializable, NetSerializable]
public sealed class DockingConsoleFTLMessage(MapId destination) : BoundUserInterfaceMessage
{
    public MapId Destination = destination;
}

[Serializable, NetSerializable]
public sealed class DockingConsoleShuttleCheckMessage : BoundUserInterfaceMessage;
