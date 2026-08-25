using Content.Shared._Onyx.Research; // <Onyx-ResearchNetworks>
using Robust.Shared.Serialization;

namespace Content.Shared.Research.Components
{
    [NetSerializable, Serializable]
    public enum ResearchConsoleUiKey : byte
    {
        Key,
    }

    [Serializable, NetSerializable]
    public sealed class ConsoleUnlockTechnologyMessage : BoundUserInterfaceMessage
    {
        public string Id;

        public ConsoleUnlockTechnologyMessage(string id)
        {
            Id = id;
        }
    }

    [Serializable, NetSerializable]
    public sealed class ConsoleServerSelectionMessage : BoundUserInterfaceMessage
    {

    }

    [Serializable, NetSerializable]
    public sealed class ResearchConsoleBoundInterfaceState : BoundUserInterfaceState
    {
        public int Points;
        public List<ResearchNetworkLogEntry> Logs; // <Onyx-ResearchNetworks>

        // <Onyx-ResearchNetworks-edited>
        public ResearchConsoleBoundInterfaceState(int points, List<ResearchNetworkLogEntry>? logs = null)
        {
            Points = points;
            Logs = logs ?? new();
        }
        // </Onyx-ResearchNetworks-edited>
    }
}
