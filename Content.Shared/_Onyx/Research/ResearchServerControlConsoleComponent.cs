using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Research;

[RegisterComponent]
public sealed partial class ResearchServerControlConsoleComponent : Component;

[Serializable, NetSerializable]
public enum ResearchServerControlUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ToggleResearchServerGenerationMessage(int serverId) : BoundUserInterfaceMessage
{
    public int ServerId = serverId;
}

[Serializable, NetSerializable]
public sealed class SetResearchServerNetworkMessage(int serverId, string networkId) : BoundUserInterfaceMessage
{
    public int ServerId = serverId;
    public string NetworkId = networkId;
}

[Serializable, NetSerializable]
public sealed class ResearchServerControlBoundInterfaceState(
    List<ResearchServerControlEntry> servers,
    List<ResearchNetworkLogEntry> logs) : BoundUserInterfaceState
{
    public List<ResearchServerControlEntry> Servers = servers;
    public List<ResearchNetworkLogEntry> Logs = logs;
}

[Serializable, NetSerializable]
public sealed class ResearchServerControlEntry(
    int id,
    string hashId,
    string name,
    string networkId,
    bool powered,
    bool authority,
    int authorityId,
    string authorityHashId,
    bool generationEnabled,
    int pointsPerSecond,
    int networkPoints)
{
    public int Id = id;
    public string HashId = hashId;
    public string Name = name;
    public string NetworkId = networkId;
    public bool Powered = powered;
    public bool Authority = authority;
    public int AuthorityId = authorityId;
    public string AuthorityHashId = authorityHashId;
    public bool GenerationEnabled = generationEnabled;
    public int PointsPerSecond = pointsPerSecond;
    public int NetworkPoints = networkPoints;
}
