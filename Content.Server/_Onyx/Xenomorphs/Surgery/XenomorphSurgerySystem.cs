using Content.Server._Onyx.Xenomorphs.Infection;
using Content.Shared._Onyx.Medical.Surgery;
using Content.Shared._Onyx.Xenomorphs.Larva;
using Content.Shared._Onyx.Xenomorphs.Surgery;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Hands.EntitySystems;

namespace Content.Server._Onyx.Xenomorphs.Surgery;

public sealed partial class XenomorphSurgerySystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryXenomorphConditionComponent, SurgeryValidEvent>(OnValid);
        SubscribeLocalEvent<SurgeryRemoveXenomorphEffectComponent, SurgeryStepEvent>(OnRemove);
        SubscribeLocalEvent<SurgeryRemoveXenomorphEffectComponent, SurgeryStepCompleteCheckEvent>(OnRemoveCheck);
    }

    private void OnValid(Entity<SurgeryXenomorphConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!TryFindTarget(args.Part, ent.Comp.Target, out _))
            args.Cancelled = true;
    }

    private void OnRemove(Entity<SurgeryRemoveXenomorphEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!TryFindTarget(args.Part, ent.Comp.Target, out var slot) ||
            !_body.TryRemoveOrgan(args.Part, slot, out var removed))
            return;

        _hands.TryPickupAnyHand(args.User, removed);
    }

    private void OnRemoveCheck(Entity<SurgeryRemoveXenomorphEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (TryFindTarget(args.Part, ent.Comp.Target, out _))
            args.Cancelled = true;
    }

    private bool TryFindTarget(
        EntityUid partUid,
        XenomorphSurgeryTarget target,
        out string slot)
    {
        slot = string.Empty;

        if (!TryComp(partUid, out BodyPartComponent? part) || part.PartType != BodyPartType.Chest)
            return false;

        foreach (var candidateSlot in part.Organs)
        {
            if (!_body.TryGetOrganInSlot(partUid, candidateSlot, out var candidate) ||
                target == XenomorphSurgeryTarget.Embryo && !HasComp<XenomorphInfectionComponent>(candidate) ||
                target == XenomorphSurgeryTarget.Larva && !HasComp<XenomorphLarvaComponent>(candidate))
                continue;

            slot = candidateSlot;
            return true;
        }

        return false;
    }
}
