using Content.Shared._Onyx.Research;
using Content.Shared.Research.Components;
using Robust.Shared.GameStates;

namespace Content.Shared.Research.Components;

public sealed partial class ResearchServerComponent
{
    public const int MaxNetworkLogs = 100;

    [DataField, AutoNetworkedField]
    public string NetworkId = "ResearchNet";

    [DataField, AutoNetworkedField]
    public bool GenerationEnabled = true;

    [DataField, AutoNetworkedField]
    public string HashId = string.Empty;

    [DataField]
    public List<ResearchNetworkLogEntry> NetworkLogs = new();
}
