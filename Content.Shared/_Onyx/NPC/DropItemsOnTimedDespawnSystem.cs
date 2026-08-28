using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Spawners;

namespace Content.Shared._Onyx.NPC;

public sealed partial class DropItemsOnTimedDespawnSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DropItemsOnTimedDespawnComponent, TimedDespawnEvent>(OnDespawn);
    }

    private void OnDespawn(Entity<DropItemsOnTimedDespawnComponent> ent, ref TimedDespawnEvent args)
    {
        if (TryComp<HandsComponent>(ent, out var hands))
            _hands.DropAll((ent, hands), checkActionBlocker: false);
    }
}
