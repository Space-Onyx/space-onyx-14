using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;

namespace Content.Shared._Onyx.Harvestable;

public sealed partial class HarvestableSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HarvestableComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<HarvestableComponent, HarvestedDoAfterEvent>(OnHarvested);
    }

    private void OnInteractHand(Entity<HarvestableComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay,
            new HarvestedDoAfterEvent(), ent.Owner, ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 1.5f,
            RequireCanInteract = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            NeedHand = true,
        });
    }

    private void OnHarvested(Entity<HarvestableComponent> ent, ref HarvestedDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (ent.Comp.Loot != null)
        {
            var item = PredictedSpawnAtPosition(ent.Comp.Loot, Transform(args.User).Coordinates);
            _hands.TryPickup(args.User, item, _hands.GetActiveHand(args.User), false);
        }

        PredictedDel(ent.Owner);
        args.Handled = true;
    }
}
