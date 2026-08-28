using System.Linq;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Inventory;
using Content.Shared._Onyx.Wounds;
using Content.Shared._Onyx.Cybernetics;
using Content.Shared._Onyx.Body;
using Content.Shared._Onyx.Chemistry.Circulation;
using Content.Shared.Stunnable;

namespace Content.Shared.Body.Systems;

public sealed partial class BodyInventorySlotSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private WoundDamageProjectionSystem _partDamage = default!;
    [Dependency] private WoundBleedingSystem _bleeding = default!;
    [Dependency] private CyberneticsSystem _cybernetics = default!;
    [Dependency] private CirculatoryStreamSystem _circulation = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyPartComponent, OrganGotInsertedEvent>(OnPartChanged);
        SubscribeLocalEvent<BodyPartComponent, OrganGotRemovedEvent>(OnPartChanged);
        SubscribeLocalEvent<BodyAnatomyComponent, StandUpAttemptEvent>(OnStandAttempt);
    }

    private void OnPartChanged(Entity<BodyPartComponent> ent, ref OrganGotInsertedEvent args)
    {
        if (TerminatingOrDeleted(ent) || TerminatingOrDeleted(args.Target))
            return;

        _inventory.RefreshBodySlots(args.Target);
        _partDamage.OnPartInserted(ent, args.Target);
        _circulation.SynchronizeStreams(args.Target, ent.Owner);
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
        _circulation.SynchronizeStreams(args.Target);
        _cybernetics.RefreshBody(args.Target);
        if (!HasComp<BodyPartReplacementComponent>(args.Target))
            KnockDownIfMissingLeg(args.Target);
    }

    private void OnStandAttempt(Entity<BodyAnatomyComponent> ent, ref StandUpAttemptEvent args)
    {
        if (MissingRequiredParts(ent, BodyPartType.Leg))
        {
            args.Cancelled = true;
            args.Autostand = false;
        }
    }

    private void KnockDownIfMissingLeg(EntityUid body)
    {
        if (TryComp(body, out BodyAnatomyComponent? anatomy) && MissingRequiredParts((body, anatomy), BodyPartType.Leg))
            _stun.TryKnockdown(body, null, autoStand: false, drop: false, force: true);
    }

    private bool MissingRequiredParts(Entity<BodyAnatomyComponent> body, BodyPartType type)
    {
        return body.Comp.RequiredParts.TryGetValue(type, out var required) &&
               _body.GetBodyChildrenOfType(body, type).Count() < required;
    }
}
