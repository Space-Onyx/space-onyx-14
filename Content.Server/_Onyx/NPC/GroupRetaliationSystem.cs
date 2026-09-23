using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
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
        SubscribeLocalEvent<GroupRetaliationComponent, NPCRetaliatedEvent>(OnRetaliated);
    }

    private void OnRetaliated(Entity<GroupRetaliationComponent> ent, ref NPCRetaliatedEvent args)
    {
        if (args.Secondary)
            return;

        foreach (var ally in _lookup.GetEntitiesInRange<GroupRetaliationComponent>(Transform(ent).Coordinates, ent.Comp.Range))
        {
            if (!_faction.IsEntityFriendly(ent.Owner, ally.Owner) || !TryComp<NPCRetaliationComponent>(ally, out var retaliation))
                continue;

            _retaliation.TryRetaliate((ally, retaliation), args.Against, true);
        }
    }
}

public sealed class NPCRetaliatedEvent(Entity<NPCRetaliationComponent> ent, EntityUid against, bool secondary) : EntityEventArgs
{
    public readonly Entity<NPCRetaliationComponent> Ent = ent;
    public readonly EntityUid Against = against;
    public readonly bool Secondary = secondary;
}
