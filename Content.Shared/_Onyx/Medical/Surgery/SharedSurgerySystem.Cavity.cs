using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Item;
using Robust.Shared.Containers;

namespace Content.Shared._Onyx.Medical.Surgery;

public abstract partial class SharedSurgerySystem
{
    private void OnCavityConditionValid(Entity<SurgeryCavityConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (CavityOccupied(args.Part) != ent.Comp.Occupied)
            args.Cancelled = true;
    }

    private void OnInsertCavityItem(Entity<SurgeryInsertCavityItemEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_net.IsServer || FindHeldCavityItem(args.Tools) is not { } item)
            return;

        var container = _containers.EnsureContainer<ContainerSlot>(args.Part, CavityContainer);
        _containers.Insert(item, container);
    }

    private void OnInsertCavityItemCheck(Entity<SurgeryInsertCavityItemEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (!CavityOccupied(args.Part))
            args.Cancelled = true;
    }

    private void OnInsertCavityItemCanPerform(Entity<SurgeryInsertCavityItemEffectComponent> ent, ref SurgeryCanPerformStepEvent args)
    {
        if (FindHeldCavityItem(args.Tools) is not { } item)
        {
            args.Invalid = StepInvalidReason.MissingTool;
            args.Popup = Loc.GetString("surgery-ui-reason-cavity-item");
            return;
        }

        args.ValidTools ??= new HashSet<EntityUid>();
        args.ValidTools.Add(item);
    }

    private void OnRemoveCavityItem(Entity<SurgeryRemoveCavityItemEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!_net.IsServer || !_containers.TryGetContainer(args.Part, CavityContainer, out var container) ||
            container is not ContainerSlot { ContainedEntity: { } item } || !_containers.Remove(item, container))
            return;

        _hands.TryPickupAnyHand(args.User, item);
    }

    private void OnRemoveCavityItemCheck(Entity<SurgeryRemoveCavityItemEffectComponent> ent, ref SurgeryStepCompleteCheckEvent args)
    {
        if (CavityOccupied(args.Part))
            args.Cancelled = true;
    }

    private EntityUid? FindHeldCavityItem(List<EntityUid> held)
    {
        EntityUid? found = null;
        var max = _item.GetSizePrototype("Small");
        foreach (var item in held)
        {
            if (!TryComp(item, out ItemComponent? itemComp) || _item.GetSizePrototype(itemComp.Size) > max ||
                HasComp<BodyPartComponent>(item) || HasComp<OrganComponent>(item))
                continue;

            if (found != null)
                return null;
            found = item;
        }
        return found;
    }

    private bool CavityOccupied(EntityUid part)
    {
        return _containers.TryGetContainer(part, CavityContainer, out var container) &&
               container is ContainerSlot { ContainedEntity: not null };
    }
}
