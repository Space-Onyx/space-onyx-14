using Content.Shared.Hands.EntitySystems;
using Content.Shared.Hands.Components; // <Onyx-FlexibleAnatomy>
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

        if (!TryComp(args.Target, out HandsComponent? hands))
            return;

        var ownership = EnsureComp<HandOrganOwnershipComponent>(args.Target);
        if (ownership.Hands.TryGetValue(ent.Owner, out var existing))
        {
            if (_hands.TryGetHand((args.Target, hands), existing, out _))
                return;
            ownership.Hands.Remove(ent.Owner);
            ent.Comp.RuntimeHandID = null;
            Dirty(ent);
        }

        var handId = ent.Comp.HandID + "-" + ent.Owner.Id;
        var suffix = 0;
        while (_hands.TryGetHand((args.Target, hands), handId, out _) || ownership.Hands.ContainsValue(handId))
        {
            suffix++;
            handId = ent.Comp.HandID + "-" + ent.Owner.Id + "-" + suffix;
        }

        ent.Comp.RuntimeHandID = handId;
        ownership.Hands[ent.Owner] = handId;
        Dirty(ent);
        _hands.AddHand(args.Target, handId, ent.Comp.Data);
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

        if (!TryComp(args.Target, out HandOrganOwnershipComponent? ownership) ||
            !ownership.Hands.Remove(ent.Owner, out var handId))
            return;

        _hands.RemoveHand(args.Target, handId);
        ent.Comp.RuntimeHandID = null;
        Dirty(ent);
        if (ownership.Hands.Count == 0)
            RemComp<HandOrganOwnershipComponent>(args.Target);
    }
    // </Onyx-Surgery>
}
