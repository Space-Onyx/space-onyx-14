using Content.Shared.Shuttles.BUIStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.FireControl;

[Serializable, NetSerializable]
public sealed class FireControlConsoleBoundInterfaceState(
    bool connected,
    FireControllableEntry[] fireControllables,
    NavInterfaceState navState) : BoundUserInterfaceState
{
    public readonly bool Connected = connected;
    public readonly FireControllableEntry[] FireControllables = fireControllables;
    public readonly NavInterfaceState NavState = navState;
}

[Serializable, NetSerializable]
public enum FireControlConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class FireControlConsoleRefreshServerMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class FireControlConsoleFireMessage(List<NetEntity> selected, NetCoordinates coordinates) : BoundUserInterfaceMessage
{
    public readonly List<NetEntity> Selected = selected;
    public readonly NetCoordinates Coordinates = coordinates;
}

[Serializable, NetSerializable]
public readonly record struct FireControllableEntry(NetEntity NetEntity, NetCoordinates Coordinates, string Name);
