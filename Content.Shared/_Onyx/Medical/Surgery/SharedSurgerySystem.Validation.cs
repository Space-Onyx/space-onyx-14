using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Buckle.Components;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Shared._Onyx.Medical.Surgery;

public abstract partial class SharedSurgerySystem
{
    protected bool IsSurgeryValid(EntityUid body, EntityUid targetPart, EntProtoId surgery, EntProtoId stepId, List<EntityUid> tools, out Entity<SurgeryComponent> surgeryEnt, out Entity<BodyPartComponent> part, out EntityUid step)
    {
        surgeryEnt = default;
        part = default;
        step = default;

        if (!HasComp<SurgeryTargetComponent>(body) || !IsReadyForSurgery(body) ||
            !TryComp(targetPart, out BodyPartComponent? partComp) || !IsPartOfTarget(body, targetPart) ||
            GetSurgeryEntity(surgery) is not { } surgeryEntId || !TryComp(surgeryEntId, out SurgeryComponent? surgeryComp) ||
            !GetSurgerySteps(body, targetPart, (surgeryEntId, surgeryComp), tools).Contains(stepId) ||
            GetSurgeryStepEntity(stepId) is not { } stepEnt)
            return false;

        var ev = new SurgeryValidEvent(body, targetPart);
        RaiseLocalEvent(stepEnt, ref ev);
        RaiseLocalEvent(surgeryEntId, ref ev);
        if (ev.Cancelled)
            return false;

        surgeryEnt = (surgeryEntId, surgeryComp);
        part = (targetPart, partComp);
        step = stepEnt;
        return true;
    }

    private bool IsLyingDown(EntityUid entity)
    {
        if (_standing.IsDown(entity))
            return true;

        return TryComp(entity, out BuckleComponent? buckle) &&
               TryComp(buckle.BuckledTo, out StrapComponent? strap) &&
               strap.Position == StrapPosition.Down;
    }

    public bool IsReadyForSurgery(EntityUid entity)
    {
        return TryComp(entity, out BodyPartComponent? part)
            ? part.Body == null && part.Parent == null
            : IsLyingDown(entity);
    }

    protected bool IsPartOfTarget(EntityUid target, EntityUid part)
    {
        return TryComp(target, out BodyPartComponent? targetPart)
            ? targetPart.Body == null && targetPart.Parent == null && _body.GetBodyPartChildren(target).Any(child => child.Id == part)
            : _body.BodyHasChild(target, part);
    }

    protected (Entity<SurgeryComponent> Surgery, int Step)? GetNextStep(EntityUid body, EntityUid part, Entity<SurgeryComponent?> surgery, List<EntityUid> requirements, List<EntityUid> tools)
    {
        if (!Resolve(surgery, ref surgery.Comp))
            return null;

        if (requirements.Contains(surgery))
            throw new ArgumentException($"Surgery {surgery} has a requirement loop");

        requirements.Add(surgery);
        if (surgery.Comp.Requirement is { } requirementId && GetSurgeryEntity(requirementId) is { } requirement &&
            TryComp(requirement, out SurgeryComponent? requirementComp) &&
            !IsSurgeryComplete(body, part, (requirement, requirementComp), tools) &&
            GetNextStep(body, part, (requirement, requirementComp), requirements, tools) is { } requiredNext)
            return requiredNext;

        var steps = GetSurgerySteps(body, part, (surgery, surgery.Comp), tools);
        for (var i = 0; i < steps.Count; i++)
            if (!IsStepComplete(body, part, steps[i]))
                return ((surgery, surgery.Comp), i);

        return null;
    }

    private bool PreviousStepsComplete(EntityUid body, EntityUid part, Entity<SurgeryComponent> surgery, EntProtoId step, List<EntityUid> tools)
    {
        if (surgery.Comp.Requirement is { } requirement &&
            (GetSurgeryEntity(requirement) is not { } requiredEnt ||
             !TryComp(requiredEnt, out SurgeryComponent? requiredComp) ||
             !IsSurgeryComplete(body, part, (requiredEnt, requiredComp), tools)))
            return false;

        foreach (var surgeryStep in GetSurgerySteps(body, part, surgery, tools))
        {
            if (surgeryStep == step)
                break;
            if (!IsStepComplete(body, part, surgeryStep))
                return false;
        }

        return true;
    }

    private bool IsSurgeryComplete(EntityUid body, EntityUid part, Entity<SurgeryComponent> surgery, List<EntityUid> tools)
    {
        return GetSurgerySteps(body, part, surgery, tools).All(step => IsStepComplete(body, part, step));
    }

    protected IReadOnlyList<EntProtoId> GetSurgerySteps(EntityUid body, EntityUid part,
        Entity<SurgeryComponent> surgery, List<EntityUid> tools)
    {
        if (surgery.Comp.Steps.Count == 0)
            return [];

        var fallback = surgery.Comp.Steps.Values.FirstOrDefault(sequence => sequence.Required.Count == 0)?.Steps ?? [];
        if (surgery.Comp.Steps.Values.All(sequence => sequence.Required.Count == 0))
            return fallback;

        var contextEvent = new SurgeryGetStepSequenceContextEvent(body, part, tools);
        foreach (var stepId in surgery.Comp.Steps.Values.SelectMany(sequence => sequence.Steps))
        {
            if (GetSurgeryStepEntity(stepId) is not { } step)
                continue;

            RaiseLocalEvent(step, ref contextEvent);
            if (contextEvent.Context != null)
                break;
        }

        if (contextEvent.Context is not { } context)
            return fallback;

        return surgery.Comp.Steps
            .Where(entry => entry.Value.Required.Count > 0 &&
                entry.Value.Required.Values.All(required => HasComp(context, required.Component.GetType())))
            .OrderByDescending(entry => entry.Value.Required.Count)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => (IReadOnlyList<EntProtoId>) entry.Value.Steps)
            .FirstOrDefault() ?? fallback;
    }

    protected bool IsStepComplete(EntityUid body, EntityUid part, EntProtoId stepId)
    {
        if (GetSurgeryStepEntity(stepId) is not { } step)
            return false;

        var ev = new SurgeryStepCompleteCheckEvent(body, part);
        RaiseLocalEvent(step, ref ev);
        return !ev.Cancelled;
    }

    protected bool CanPerformStep(EntityUid user, EntityUid body, EntityUid targetPart, BodyPartType part, EntityUid step, bool doPopup, out string? popup, out StepInvalidReason reason, out HashSet<EntityUid>? validTools)
    {
        if (!_interaction.InRangeUnobstructed(user, body, popup: doPopup))
        {
            popup = "You are too far away from the patient.";
            reason = StepInvalidReason.OutOfRange;
            validTools = null;
            return false;
        }

        var slot = part switch
        {
            BodyPartType.Head => SlotFlags.HEAD,
            BodyPartType.Chest or BodyPartType.Groin or BodyPartType.Arm => SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING,
            BodyPartType.Hand => SlotFlags.GLOVES,
            BodyPartType.Leg => SlotFlags.OUTERCLOTHING | SlotFlags.LEGS,
            BodyPartType.Foot => SlotFlags.FEET,
            _ => SlotFlags.NONE,
        };

        if (slot != SlotFlags.NONE && TryComp(body, out InventoryComponent? inventory))
        {
            var equipped = new InventorySystem.InventorySlotEnumerator(inventory, slot);
            if (equipped.NextItem(out _))
            {
                popup = "Remove clothing covering the surgical site.";
                validTools = null;
                if (doPopup)
                    _popup.PopupEntity(popup, user, PopupType.SmallCaution);
                reason = StepInvalidReason.Clothing;
                return false;
            }
        }

        var check = new SurgeryCanPerformStepEvent(user, body, targetPart, GetActiveTool(user), slot);
        RaiseLocalEvent(step, ref check);
        popup = check.Popup;
        validTools = check.ValidTools;

        if (check.Invalid == StepInvalidReason.None)
        {
            reason = default;
            return true;
        }

        if (doPopup && check.Popup != null)
            _popup.PopupEntity(check.Popup, user, PopupType.SmallCaution);

        reason = check.Invalid;
        return false;
    }

    private bool CanPerformStep(EntityUid user, EntityUid body, EntityUid targetPart, BodyPartType part, EntityUid step, bool doPopup)
        => CanPerformStep(user, body, targetPart, part, step, doPopup, out _, out _, out _);

    protected virtual void RefreshUI(EntityUid body) { }
}
