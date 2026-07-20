using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Inventory;
using Content.Shared._Onyx.Wounds;
using Content.Shared._Onyx.Cybernetics;

namespace Content.Shared.Body.Systems;

public sealed partial class BodyInventorySlotSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private WoundDamageProjectionSystem _partDamage = default!;
    [Dependency] private WoundBleedingSystem _bleeding = default!;
    [Dependency] private CyberneticsSystem _cybernetics = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyPartComponent, OrganGotInsertedEvent>(OnPartChanged);
        SubscribeLocalEvent<BodyPartComponent, OrganGotRemovedEvent>(OnPartChanged);
    }

    private void OnPartChanged(Entity<BodyPartComponent> ent, ref OrganGotInsertedEvent args)
    {
        if (TerminatingOrDeleted(ent) || TerminatingOrDeleted(args.Target))
            return;

        _inventory.RefreshBodySlots(args.Target);
        _partDamage.OnPartInserted(ent, args.Target);
        _bleeding.OnPartInserted(ent, args.Target);
        _cybernetics.RefreshBody(args.Target);
    }

    private void OnPartChanged(Entity<BodyPartComponent> ent, ref OrganGotRemovedEvent args)
    {
        if (TerminatingOrDeleted(ent) || TerminatingOrDeleted(args.Target))
            return;

        _inventory.RefreshBodySlots(args.Target);
        _partDamage.OnPartRemoved(ent, args.Target);
        _bleeding.OnPartChanged(args.Target);
        _cybernetics.RefreshBody(args.Target);
    }
}
