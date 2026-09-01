using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Content.Shared.Prototypes;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Shared._Onyx.Medical.Surgery;

public abstract partial class SharedSurgerySystem
{
    private void OnRemoveOrgan(Entity<SurgeryRemoveOrganEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_net.IsServer ||
            !TryFindMatchingOrgan(args.Part, ent.Comp.Slot, ent.Comp.Required, out _, out var slot) ||
            !_body.TryRemoveOrgan(args.Part, slot, out var organ))
            return;

        _hands.TryPickupAnyHand(args.User, organ);
    }

    private void OnRemoveOrganCheck(Entity<SurgeryRemoveOrganEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (TryFindMatchingOrgan(args.Part, ent.Comp.Slot, ent.Comp.Required, out _, out _))
            args.Cancelled = true;
    }

    private void OnInsertOrgan(Entity<SurgeryInsertOrganEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_net.IsServer || FindHeldOrgan(args.Tools, ent.Comp.Slot, ent.Comp.RequireMechanical, ent.Comp.Required) is not { } organ)
            return;

        if (_body.TryInsertOrgan(args.Part, organ, ent.Comp.Slot.Id))
        {
            var inserted = new SurgeryOrganInsertedEvent(args.User, args.Body, args.Part);
            RaiseLocalEvent(organ, ref inserted);
        }
    }

    private void OnInsertOrganCheck(Entity<SurgeryInsertOrganEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!_body.TryGetOrganInSlot(args.Part, ent.Comp.Slot.Id, out _))
            args.Cancelled = true;
    }

    private void OnInsertOrganCanPerform(Entity<SurgeryInsertOrganEffectComponent> ent, ref SurgeryCanPerformStepEvent args)
    {
        if (FindHeldOrgan(args.Tools, ent.Comp.Slot, ent.Comp.RequireMechanical, ent.Comp.Required) is not { } organ)
        {
            SetMissingTool(ref args, "surgery-ui-reason-organ");
            return;
        }

        if (!_body.AreTransplantsCompatible(args.Part, organ))
        {
            args.Invalid = StepInvalidReason.IncompatibleTransplant;
            args.Popup = Loc.GetString("surgery-ui-reason-incompatible-transplant");
            return;
        }

        args.ValidTools ??= new HashSet<EntityUid>();
        args.ValidTools.Add(organ);
    }

    private void OnInsertOrganGetSequenceContext(Entity<SurgeryInsertOrganEffectComponent> ent, ref SurgeryGetStepSequenceContextEvent args)
    {
        if (_body.TryGetOrganInSlot(args.Part, ent.Comp.Slot.Id, out var inserted))
        {
            args.Context = inserted;
            return;
        }

        args.Context = FindHeldOrgan(args.Tools, ent.Comp.Slot, ent.Comp.RequireMechanical, ent.Comp.Required);
    }

    private EntityUid? FindHeldOrgan(List<EntityUid> held, ProtoId<OrganCategoryPrototype> slot, bool requireMechanical,
        ComponentRegistry? required)
    {
        EntityUid? found = null;
        foreach (var item in held)
        {
            if (HasComp<BodyPartComponent>(item) || !TryComp(item, out OrganComponent? organ) || organ.Body != null ||
                organ.Category != slot || requireMechanical && !HasComp<MechanicalOrganComponent>(item) ||
                required != null && required.Values.Any(component => !HasComp(item, component.Component.GetType())))
                continue;

            if (found != null)
                return null;
            found = item;
        }

        return found;
    }

    private void OnOrganHealValid(Entity<SurgeryOrganHealEffectComponent> ent, ref SurgeryValidEvent args)
    {
        if (ent.Comp.Amount <= FixedPoint2.Zero || !TryFindOrgan(args.Part, ent.Comp.Slot, out var organ) ||
            organ.Comp.Health >= organ.Comp.MaxHealth)
            args.Cancelled = true;
    }

    private void OnOrganHeal(Entity<SurgeryOrganHealEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_net.IsServer || !TryFindOrgan(args.Part, ent.Comp.Slot, out var organ))
            return;

        _organHealth.ChangeHealth(organ, ent.Comp.Amount);
    }

    private void OnOrganHealCheck(Entity<SurgeryOrganHealEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!TryFindOrgan(args.Part, ent.Comp.Slot, out var organ) || organ.Comp.Health < organ.Comp.MaxHealth)
            args.Cancelled = true;
    }

    private bool TryFindOrgan(EntityUid part, ProtoId<OrganCategoryPrototype> slot, out Entity<OrganComponent> organ)
    {
        organ = default;
        if (!_body.TryGetOrganInSlot(part, slot, out var organId) || !TryComp(organId, out OrganComponent? component))
            return false;

        organ = (organId, component);
        return true;
    }

    private bool TryFindMatchingOrgan(
        EntityUid part,
        ProtoId<OrganCategoryPrototype>? slot,
        ComponentRegistry required,
        out Entity<OrganComponent> organ,
        out string organSlot)
    {
        organ = default;
        organSlot = string.Empty;
        if (!TryComp(part, out BodyPartComponent? bodyPart))
            return false;

        foreach (var candidateSlot in bodyPart.Organs)
        {
            if (slot is { } category && candidateSlot != category.Id ||
                !_body.TryGetOrganInSlot(part, candidateSlot, out var candidate) ||
                !TryComp(candidate, out OrganComponent? component) ||
                !HasRequiredComponents(candidate, required))
                continue;

            if (organ.Owner.Valid)
                return false;

            organ = (candidate, component);
            organSlot = candidateSlot;
        }

        return organ.Owner.Valid;
    }

    private bool HasRequiredComponents(EntityUid entity, ComponentRegistry required) =>
        required.Values.All(component => HasComp(entity, component.Component.GetType()));
}
