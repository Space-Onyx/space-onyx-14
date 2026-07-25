using Content.Shared.EntityEffects;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.NPC.FactionStatusEffects;

public sealed partial class SetFactionEntityEffectSystem
    : EntityEffectSystem<NpcFactionMemberComponent, SetFaction>
{
    [Dependency] private NpcFactionSystem _factions = default!;

    protected override void Effect(Entity<NpcFactionMemberComponent> entity, ref EntityEffectEvent<SetFaction> args)
    {
        _factions.ClearFactions((entity.Owner, entity.Comp), false);
        _factions.AddFaction((entity.Owner, entity.Comp), args.Effect.Faction);
    }
}

public sealed partial class SetFaction : EntityEffectBase<SetFaction>
{
    [DataField(required: true)]
    public ProtoId<NpcFactionPrototype> Faction;
}
