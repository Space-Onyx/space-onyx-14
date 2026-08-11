using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.NPC.Systems;

namespace Content.Server._Onyx.NPC;

public sealed partial class GroupRetaliationSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private NPCRetaliationSystem _retaliation = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GroupRetaliationComponent, DamageChangedEvent>(OnDamaged);
    }

    private void OnDamaged(Entity<GroupRetaliationComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin is not { } attacker)
            return;

        foreach (var ally in _lookup.GetEntitiesInRange<GroupRetaliationComponent>(Transform(ent).Coordinates, ent.Comp.Range))
        {
            if (!_faction.IsEntityFriendly(ent.Owner, ally.Owner) || !TryComp<NPCRetaliationComponent>(ally, out var retaliation))
                continue;

            _retaliation.TryRetaliate((ally, retaliation), attacker);
        }
    }
}
