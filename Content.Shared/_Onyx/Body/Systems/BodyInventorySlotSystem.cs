using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Inventory;
using Content.Shared._Onyx.Wounds;
using Content.Shared._Onyx.Cybernetics;
using Content.Shared._Onyx.Body;
using Content.Shared.Stunnable;

namespace Content.Shared.Body.Systems;

public sealed partial class BodyInventorySlotSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private WoundDamageProjectionSystem _partDamage = default!;
    [Dependency] private WoundBleedingSystem _bleeding = default!;
    [Dependency] private CyberneticsSystem _cybernetics = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyPartComponent, OrganGotInsertedEvent>(OnPartChanged);
        SubscribeLocalEvent<BodyPartComponent, OrganGotRemovedEvent>(OnPartChanged);
        SubscribeLocalEvent<InitiallyLeggedComponent, StandUpAttemptEvent>(OnStandAttempt);
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
        if (!HasComp<BodyPartReplacementComponent>(args.Target))
            KnockDownIfMissingLeg(args.Target);
    }

    private void OnStandAttempt(Entity<InitiallyLeggedComponent> ent, ref StandUpAttemptEvent args)
    {
        if (_body.GetBodyChildrenOfType(ent, BodyPartType.Leg).Count() < ent.Comp.InitialLegCount)
            args.Cancelled = true;
    }

    private void KnockDownIfMissingLeg(EntityUid body)
    {
        if (TryComp(body, out InitiallyLeggedComponent? legged) &&
            _body.GetBodyChildrenOfType(body, BodyPartType.Leg).Count() < legged.InitialLegCount)
            _stun.TryKnockdown(body, null, autoStand: false, drop: false, force: true);
    }
}
