using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.NPC.FactionStatusEffects;

[RegisterComponent]
public sealed partial class FactionOverrideStatusEffectComponent : Component
{
    [DataField(required: true)]
    public ProtoId<NpcFactionPrototype> Faction;
}

[RegisterComponent]
public sealed partial class FactionOverrideStateComponent : Component
{
    public HashSet<ProtoId<NpcFactionPrototype>> OriginalFactions = [];

    public List<EntityUid> ActiveOverrides = [];
}
