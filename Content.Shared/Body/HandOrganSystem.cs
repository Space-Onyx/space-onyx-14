using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Network;
namespace Content.Shared.Body;

public sealed partial class HandOrganSystem : EntitySystem
{
    // <Onyx-Surgery>
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HandOrganComponent, OrganGotInsertedEvent>(OnGotInserted);
        SubscribeLocalEvent<HandOrganComponent, OrganGotRemovedEvent>(OnGotRemoved);
    }

    private void OnGotInserted(Entity<HandOrganComponent> ent, ref OrganGotInsertedEvent args)
    {
        // Server-only: on the client hands are synced via HandsComponentState.
        // Container rebuilds during PVS unload/reload must not mutate hands or drop held items.
        if (_net.IsClient)
            return;

        _hands.AddHand(args.Target, ent.Comp.HandID, ent.Comp.Data);
    }

    private void OnGotRemoved(Entity<HandOrganComponent> ent, ref OrganGotRemovedEvent args)
    {
        // Server-only: on the client hands are synced via HandsComponentState.
        // Container rebuilds during PVS unload/reload must not mutate hands or drop held items.
        if (_net.IsClient)
            return;

        // prevent a recursive double-delete bug
        if (LifeStage(args.Target) >= EntityLifeStage.Terminating)
            return;

        _hands.RemoveHand(args.Target, ent.Comp.HandID);
    }
    // </Onyx-Surgery>
}
