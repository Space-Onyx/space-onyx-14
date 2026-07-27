using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Mech;

/// <summary>
/// Stores a mech's factions while its pilot factions are relayed to it.
/// </summary>
[RegisterComponent]
public sealed partial class MechFactionRelayComponent : Component
{
    public bool HadFactionComponent;
    public HashSet<ProtoId<NpcFactionPrototype>> OriginalFactions = [];
}
