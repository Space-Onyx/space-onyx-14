using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Humanoid;
using System.Linq;

namespace Content.Shared._Onyx.Medical.Surgery;

public abstract partial class SharedSurgerySystem
{
    private void OnMarkerConditionValid(Entity<SurgeryMarkerConditionComponent> ent, ref SurgeryValidEvent args)
    {
        var markers = CompOrNull<SurgeryMarkerComponent>(args.Part)?.Markers;
        if (ent.Comp.All.Any(marker => markers == null || !markers.Contains(marker)) ||
            ent.Comp.Any.Count > 0 && (markers == null || !ent.Comp.Any.Any(markers.Contains)) ||
            markers != null && ent.Comp.None.Any(markers.Contains) ||
            ent.Comp.MissingAny.Count > 0 && markers != null && ent.Comp.MissingAny.All(markers.Contains))
            args.Cancelled = true;
    }

    private void OnSpeciesConditionValid(Entity<SurgerySpeciesConditionComponent> ent, ref SurgeryValidEvent args)
    {
        var species = CompOrNull<HumanoidProfileComponent>(args.Body)?.Species ??
                      CompOrNull<BodyPartComponent>(args.Part)?.Species;
        var matches = species is { } id && ent.Comp.Species.Contains(id);
        if (matches == ent.Comp.Inverse)
            args.Cancelled = true;
    }

    private void OnOrganTagConditionValid(Entity<SurgeryOrganTagConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (ent.Comp.Inverse)
            return;

        if (!TryComp(args.Part, out BodyPartComponent? partComp)
            || partComp.Body != args.Body)
        {
            args.Cancelled = true;
            return;
        }

        var found = false;
        foreach (var (organId, _) in _body.GetPartOrgans(args.Part))
        {
            if (_tags.HasTag(organId, ent.Comp.Tag))
            {
                found = true;
                break;
            }
        }

        if (!found)
            args.Cancelled = true;
    }

    private void OnOrganTagConditionCanPerform(Entity<SurgeryOrganTagConditionComponent> ent, ref SurgeryCanPerformStepEvent args)
    {
        if (!ent.Comp.Inverse)
            return;

        foreach (var tool in args.Tools)
        {
            if (HasComp<OrganComponent>(tool) && _tags.HasTag(tool, ent.Comp.Tag))
                return;
        }

        SetMissingTool(ref args, "surgery-ui-reason-organ");
    }

    private void OnPartConditionValid(Entity<SurgeryPartConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (CompOrNull<BodyPartComponent>(args.Part) is not { } part)
        {
            args.Cancelled = true;
            return;
        }

        var matches = (ent.Comp.Part == part.PartType || ent.Comp.Parts.Contains(part.PartType)) &&
                       (ent.Comp.Symmetry is not { } symmetry || part.Symmetry == symmetry);
        if (matches == ent.Comp.Inverse)
            args.Cancelled = true;
    }

    private void OnMissingPartConditionValid(Entity<SurgeryMissingPartConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (TryGetAttachedPart(args.Part, ent.Comp.Part, ent.Comp.Symmetry, out var part) &&
            !HasComp<BodyPartReattachedComponent>(part) &&
            !HasComp<BodyPartMendedComponent>(part))
            args.Cancelled = true;
    }

    private void OnDetachablePartConditionValid(Entity<SurgeryDetachablePartConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!_body.TryGetParentBodyPart(args.Part, out _, out _))
            args.Cancelled = true;
    }

    private void OnComponentConditionValid(Entity<SurgeryComponentConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (_net.IsClient)
            return;

        var target = ent.Comp.Target == SurgeryEntityTarget.Body ? args.Body : args.Part;
        if (!ComponentsMatch(target, ent.Comp.All, ent.Comp.None))
            args.Cancelled = true;
    }

    private void OnOrganConditionValid(Entity<SurgeryOrganConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!TryComp(args.Part, out BodyPartComponent? part) ||
            ent.Comp.Part is { } requiredPart && part.PartType != requiredPart)
        {
            args.Cancelled = true;
            return;
        }

        var found = TryFindMatchingOrgan(args.Part, ent.Comp.Slot, ent.Comp.Required, out var organ, out _);
        if (found == ent.Comp.Inverse ||
            found && ent.Comp.Damaged && organ.Comp.Health >= organ.Comp.MaxHealth)
            args.Cancelled = true;
    }
}
