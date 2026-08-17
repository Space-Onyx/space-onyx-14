using Content.Shared._Onyx.Body;
using Content.Shared.Body.Part;
using Content.Shared.Damage.Components;
using Content.Shared.Standing;

namespace Content.Shared._Onyx.Medical.Surgery;

public abstract partial class SharedSurgerySystem
{
    private void OnDetachPart(Entity<SurgeryDetachPartEffectComponent> ent, ref SurgeryStepEvent args)
    {
        var removedHead = Comp<BodyPartComponent>(args.Part).PartType == BodyPartType.Head;
        if (!_net.IsServer || !_body.TryDetachPart(args.Part))
            return;

        if (removedHead)
            _standing.Down(args.Body, force: true);

        _inventory.RefreshBodySlots(args.Body);
        _hands.TryPickupAnyHand(args.User, args.Part);
    }

    private void OnDetachPartCheck(Entity<SurgeryDetachPartEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (IsPartOfTarget(args.Body, args.Part))
            args.Cancelled = true;
    }

    private void OnAttachPart(Entity<SurgeryAttachPartEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_net.IsServer || FindHeldPart(args.Tools, ent.Comp.Part, ent.Comp.Symmetry) is not { } part)
            return;

        if (_body.TryAttachPart(args.Part, part))
        {
            RemComp<BodyPartMendedComponent>(part);
            RemComp<BodyPartSuturedComponent>(part);
            EnsureComp<BodyPartReattachedComponent>(part);
            EnsurePartDamageable(part);
            _inventory.RefreshBodySlots(args.Body);
        }
    }

    private void OnTargetStandAttempt(Entity<SurgeryTargetComponent> ent, ref StandAttemptEvent args)
    {
        if (!_body.BodyHasPartType(ent, BodyPartType.Head))
            args.Cancel();
    }

    private void EnsurePartDamageable(EntityUid part)
    {
        EnsureComp<DamageableComponent>(part);
        if (HasComp<InjurableComponent>(part))
            return;

        var injurable = EnsureComp<InjurableComponent>(part);
        injurable.DamageContainer = "Biological";
        Dirty(part, injurable);
    }

    private void OnAttachPartCheck(Entity<SurgeryAttachPartEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!TryGetAttachedPart(args.Part, ent.Comp.Part, ent.Comp.Symmetry, out var part) ||
            !HasComp<BodyPartReattachedComponent>(part) &&
            !HasComp<BodyPartMendedComponent>(part) &&
            !HasComp<BodyPartSuturedComponent>(part))
            args.Cancelled = true;
    }

    private void OnMendAttachedPart(Entity<SurgeryMendAttachedPartEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_net.IsServer || !TryGetAttachedPart(args.Part, ent.Comp.Part, ent.Comp.Symmetry, out var part) ||
            !RemComp<BodyPartReattachedComponent>(part))
            return;

        EnsureComp<BodyPartMendedComponent>(part);
    }

    private void OnMendAttachedPartCheck(Entity<SurgeryMendAttachedPartEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!TryGetAttachedPart(args.Part, ent.Comp.Part, ent.Comp.Symmetry, out var part) ||
            !HasComp<BodyPartMendedComponent>(part) && !HasComp<BodyPartSuturedComponent>(part))
            args.Cancelled = true;
    }

    private void OnSutureAttachedPart(Entity<SurgerySutureAttachedPartEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_net.IsServer || !TryGetAttachedPart(args.Part, ent.Comp.Part, ent.Comp.Symmetry, out var part) ||
            !RemComp<BodyPartMendedComponent>(part))
            return;

        EnsureComp<BodyPartSuturedComponent>(part);
    }

    private void OnSutureAttachedPartCheck(Entity<SurgerySutureAttachedPartEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!TryGetAttachedPart(args.Part, ent.Comp.Part, ent.Comp.Symmetry, out var part) ||
            !HasComp<BodyPartSuturedComponent>(part))
            args.Cancelled = true;
    }

    private bool TryGetAttachedPart(EntityUid parent, BodyPartType type, BodyPartSymmetry symmetry, out EntityUid part)
    {
        foreach (var child in _body.GetBodyPartChildren(parent))
        {
            if (child.Component.PartType != type || child.Component.Symmetry != symmetry)
                continue;

            part = child.Id;
            return true;
        }

        part = default;
        return false;
    }

    private void OnAttachPartCanPerform(Entity<SurgeryAttachPartEffectComponent> ent, ref SurgeryCanPerformStepEvent args)
    {
        ValidatePartAttachment(ref args, ent.Comp.Part, ent.Comp.Symmetry);
    }

    private void OnAttachPartGetSequenceContext(Entity<SurgeryAttachPartEffectComponent> ent, ref SurgeryGetStepSequenceContextEvent args)
    {
        if (TryGetAttachedPart(args.Part, ent.Comp.Part, ent.Comp.Symmetry, out var attached))
        {
            args.Context = attached;
            return;
        }

        args.Context = FindHeldPart(args.Tools, ent.Comp.Part, ent.Comp.Symmetry);
    }

    private void ValidatePartAttachment(ref SurgeryCanPerformStepEvent args, BodyPartType type, BodyPartSymmetry symmetry)
    {
        if (FindHeldPart(args.Tools, type, symmetry) is not { } part)
        {
            args.Invalid = StepInvalidReason.MissingTool;
            args.Popup = Loc.GetString("surgery-ui-reason-part");
            return;
        }

        if (!_body.AreTransplantsCompatible(args.Part, part))
        {
            args.Invalid = StepInvalidReason.IncompatibleTransplant;
            args.Popup = Loc.GetString("surgery-ui-reason-incompatible-transplant");
            return;
        }

        if (_body.HasAmputationConsequence(args.Part))
        {
            args.Invalid = StepInvalidReason.AmputationConsequence;
            args.Popup = Loc.GetString("surgery-ui-reason-amputation-consequence");
            return;
        }

        args.ValidTools ??= new HashSet<EntityUid>();
        args.ValidTools.Add(part);
    }

    private EntityUid? FindHeldPart(List<EntityUid> held, BodyPartType type, BodyPartSymmetry symmetry)
    {
        EntityUid? found = null;
        foreach (var item in held)
        {
            if (!TryComp(item, out BodyPartComponent? part) || part.Body != null || part.PartType != type || part.Symmetry != symmetry)
                continue;

            if (found != null)
                return null;
            found = item;
        }

        return found;
    }
}
