using Content.Shared._Onyx.Body;
using Content.Shared._Onyx.Body.Prototypes;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Containers;
using System.Linq;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared.Body.Systems;

public sealed partial class SharedBodySystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<BodyComponent, OrganInsertedIntoEvent>(OnOrganInserted);
        SubscribeLocalEvent<BodyComponent, OrganRemovedFromEvent>(OnOrganRemoved);
        SubscribeLocalEvent<BodyPartComponent, EntInsertedIntoContainerMessage>(OnPartInserted);
        SubscribeLocalEvent<BodyPartComponent, EntRemovedFromContainerMessage>(OnPartRemoved);
    }

    private void OnOrganInserted(Entity<BodyComponent> body, ref OrganInsertedIntoEvent args)
    {
        if (TryComp(args.Organ, out BodyPartComponent? part))
            SetSubtreeBody((args.Organ, part), body);
    }

    private void OnOrganRemoved(Entity<BodyComponent> body, ref OrganRemovedFromEvent args)
    {
        if (TryComp(args.Organ, out BodyPartComponent? removedPart))
        {
            removedPart.Parent = null;
            Dirty(args.Organ, removedPart);
            SetSubtreeBody((args.Organ, removedPart), null);
        }
    }

    private void OnPartInserted(Entity<BodyPartComponent> parent, ref EntInsertedIntoContainerMessage args)
    {
        if (TryComp(args.Entity, out BodyPartComponent? part))
        {
            part.Parent = parent;
            Dirty(args.Entity, part);
            SetSubtreeBody((args.Entity, part), parent.Comp.Body);
        }
        else if (TryComp(args.Entity, out OrganComponent? organ))
            SetOrganBody((args.Entity, organ), parent.Comp.Body);
    }

    private void OnPartRemoved(Entity<BodyPartComponent> parent, ref EntRemovedFromContainerMessage args)
    {
        if (TryComp(args.Entity, out BodyPartComponent? part))
        {
            part.Parent = null;
            Dirty(args.Entity, part);
            SetSubtreeBody((args.Entity, part), null);
        }
        else if (TryComp(args.Entity, out OrganComponent? organ))
            SetOrganBody((args.Entity, organ), null);
    }

    private void SetSubtreeBody(Entity<BodyPartComponent> root, EntityUid? body)
    {
        foreach (var (partId, part) in GetBodyPartChildren(root))
        {
            TryComp(partId, out OrganComponent? organ);
            var oldBody = part.Body ?? organ?.Body;
            var changed = false;
            if (part.Body != body)
            {
                part.Body = body;
                Dirty(partId, part);
                changed = true;
            }

            if (organ != null && organ.Body != body)
            {
                organ.Body = body;
                Dirty(partId, organ);
                changed = true;
            }

            if (changed)
            {
                if (body is { } insertedBody)
                {
                    var added = new OrganGotInsertedEvent(insertedBody);
                    RaiseLocalEvent(partId, ref added);
                }
                else if (oldBody is { } removedBody)
                {
                    var removed = new OrganGotRemovedEvent(removedBody);
                    RaiseLocalEvent(partId, ref removed);
                }
            }

            foreach (var slot in part.Organs)
            {
                if (!_containers.TryGetContainer(partId, BodyPartComponent.OrganSlotPrefix + slot, out var container))
                    continue;

                foreach (var organId in container.ContainedEntities)
                {
                    if (TryComp(organId, out OrganComponent? childOrgan))
                        SetOrganBody((organId, childOrgan), body);
                }
            }
        }
    }

    private void SetOrganBody(Entity<OrganComponent> organ, EntityUid? body)
    {
        if (organ.Comp.Body == body)
            return;

        var oldBody = organ.Comp.Body;
        organ.Comp.Body = body;
        Dirty(organ);
        if (body is { } insertedBody)
        {
            var added = new OrganGotInsertedEvent(insertedBody);
            RaiseLocalEvent(organ, ref added);
        }
        else if (oldBody is { } removedBody)
        {
            var removed = new OrganGotRemovedEvent(removedBody);
            RaiseLocalEvent(organ, ref removed);
        }
    }

    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildren(EntityUid body)
    {
        if (!TryComp(body, out BodyComponent? bodyComponent) || bodyComponent.RootContainer?.ContainedEntity is not { } root)
            yield break;

        foreach (var part in GetBodyPartChildren(root))
            yield return part;
    }

    public IEnumerable<(EntityUid Id, OrganComponent Component)> GetBodyOrgans(EntityUid body)
    {
        foreach (var (partId, part) in GetBodyChildren(body))
        {
            foreach (var slot in part.Organs)
            {
                if (!_containers.TryGetContainer(partId, BodyPartComponent.OrganSlotPrefix + slot, out var container))
                    continue;

                foreach (var organ in container.ContainedEntities)
                {
                    if (TryComp(organ, out OrganComponent? component))
                        yield return (organ, component);
                }
            }
        }
    }

    public void InitializeAnatomy(EntityUid body)
    {
        var anatomy = EnsureComp<BodyAnatomyComponent>(body);
        if (anatomy.AnatomyInitialized)
            return;

        foreach (var (partId, part) in GetBodyChildren(body))
        {
            anatomy.RequiredParts[part.PartType] = anatomy.RequiredParts.GetValueOrDefault(part.PartType) + 1;
            if (TryComp(partId, out OrganComponent? organ) && organ.Category is { } category)
                anatomy.RequiredOrgans[category] = anatomy.RequiredOrgans.GetValueOrDefault(category) + 1;
        }

        foreach (var (_, organ) in GetBodyOrgans(body))
            if (organ.Category is { } category)
                anatomy.RequiredOrgans[category] = anatomy.RequiredOrgans.GetValueOrDefault(category) + 1;

        anatomy.AnatomyInitialized = true;
        Dirty(body, anatomy);

        if (anatomy.RequiredOrgans.ContainsKey(new ProtoId<OrganCategoryPrototype>("Lungs")))
            EnsureComp<InitiallyLungedComponent>(body);
    }

    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildrenOfType(EntityUid body, BodyPartType type)
    {
        foreach (var part in GetBodyChildren(body))
            if (part.Component.PartType == type)
                yield return part;
    }

    public bool BodyHasChild(EntityUid body, EntityUid part)
    {
        return GetBodyChildren(body).Any(child => child.Id == part);
    }

    public bool BodyHasPartType(EntityUid body, BodyPartType type) => GetBodyChildrenOfType(body, type).Any();

    public bool TryGetParentBodyPart(EntityUid part, out EntityUid? parent, out BodyPartComponent? parentPart)
    {
        parent = null;
        parentPart = null;
        if (!TryComp(part, out BodyPartComponent? bodyPart) || bodyPart.Parent is not { } parentId || !TryComp(parentId, out parentPart))
            return false;

        parent = parentId;
        return true;
    }

    public bool HasAmputationConsequence(EntityUid part)
    {
        if (!TryComp(part, out BodyPartComponent? bodyPart) || bodyPart.Body is not { } body ||
            !TryComp(body, out WoundHostComponent? host) ||
            !_containers.TryGetContainer(part, WoundableComponent.ContainerId, out var container))
            return false;

        foreach (var entity in container.ContainedEntities)
        {
            if (TryComp(entity, out WoundComponent? wound) && wound.Prototype == host.AmputationConsequenceWound)
                return true;
        }

        return false;
    }

    public bool TryDetachPart(EntityUid part, bool reparent = true)
    {
        if (!TryGetParentBodyPart(part, out var parent, out var parentPart) || parent == null || parentPart == null)
            return false;

        var body = Comp<BodyPartComponent>(part).Body;

        foreach (var slot in parentPart.Children.Keys.ToList())
        {
            if (!_containers.TryGetContainer(parent.Value, BodyPartComponent.PartSlotPrefix + slot, out var container) || container is not ContainerSlot { ContainedEntity: { } child } || child != part)
                continue;

            if (!_containers.Remove(part, container, reparent: reparent))
                return false;

            if (!parentPart.ChildSlots.ContainsKey(slot))
                parentPart.Children.Remove(slot);
            Dirty(parent.Value, parentPart);
            return true;
        }

        return false;
    }

    public bool TryAttachPart(EntityUid parentId, EntityUid partId)
    {
        if (!TryComp(parentId, out BodyPartComponent? parent) || !TryComp(partId, out BodyPartComponent? part) ||
            !HasComp<OrganComponent>(partId) || part.Body != null ||
            !AreTransplantsCompatible(parentId, partId) || HasAmputationConsequence(parentId))
            return false;

        if (parent.ChildSlots.Count > 0)
        {
            string? matching = null;
            foreach (var (candidate, descriptor) in parent.ChildSlots)
            {
                if (descriptor.Type != part.PartType || descriptor.Symmetry != part.Symmetry
                    || IsPartSlotOccupied(parentId, candidate))
                    continue;
                if (matching != null)
                    return false;
                matching = candidate;
            }

            return matching != null && TryAttachPart(parentId, matching, partId);
        }

        var slot = GetPartSlot(parent, part);
        if (slot == null || parent.Children.ContainsKey(slot))
            return false;

        var container = _containers.EnsureContainer<ContainerSlot>(parentId, BodyPartComponent.PartSlotPrefix + slot);
        if (container.ContainedEntity != null)
            return false;

        if (!_containers.Insert(partId, container))
            return false;

        parent.Children[slot] = part.PartType;
        part.Parent = parentId;
        Dirty(parentId, parent);
        Dirty(partId, part);
        return true;
    }

    public bool TryRemoveOrgan(EntityUid partId, string slot, out EntityUid organ, bool reparent = true)
    {
        organ = default;
        if (!TryComp(partId, out BodyPartComponent? part)
            || !_containers.TryGetContainer(partId, BodyPartComponent.OrganSlotPrefix + slot, out var container)
            || container is not ContainerSlot { ContainedEntity: { } contained })
            return false;

        if (!_containers.Remove(contained, container, reparent: reparent))
            return false;

        organ = contained;
        if (part.Body is { } body)
            RaiseLocalEvent(body, new BodyOrganSlotChangedEvent(slot, organ, false));
        return true;
    }

    public bool TryInsertOrgan(EntityUid partId, EntityUid organId, string slot)
    {
        return TryInsertOrgan(partId, organId, slot, true);
    }

    public bool TryInsertOrganIgnoringCompatibility(EntityUid partId, EntityUid organId, string slot)
    {
        return TryInsertOrgan(partId, organId, slot, false);
    }

    private bool TryInsertOrgan(EntityUid partId, EntityUid organId, string slot, bool checkCompatibility)
    {
        if (!CanInsertOrgan(partId, organId, slot, false, checkCompatibility, out var part))
            return false;

        var container = _containers.EnsureContainer<ContainerSlot>(partId, BodyPartComponent.OrganSlotPrefix + slot);
        if (container.ContainedEntity != null || !_containers.Insert(organId, container))
            return false;

        if (part.Organs.Add(slot))
            Dirty(partId, part);

        if (part.Body is { } body)
            RaiseLocalEvent(body, new BodyOrganSlotChangedEvent(slot, organId, true));

        return true;
    }

    public bool CanInsertOrgan(EntityUid partId, EntityUid organId, string slot, bool ignoreOccupied = false) =>
        CanInsertOrgan(partId, organId, slot, ignoreOccupied, true, out _);

    private bool CanInsertOrgan(EntityUid partId, EntityUid organId, string slot, bool ignoreOccupied,
        bool checkCompatibility, out BodyPartComponent part)
    {
        part = default!;
        if (!TryComp(partId, out BodyPartComponent? foundPart)
            || !TryComp(organId, out OrganComponent? organ)
            || organ.Body != null
            || organ.Category is { } category && category.Id != slot
            || checkCompatibility && !AreTransplantsCompatible(partId, organId)
            || !ignoreOccupied && _containers.TryGetContainer(partId, BodyPartComponent.OrganSlotPrefix + slot, out var container) &&
                container is ContainerSlot { ContainedEntity: not null })
            return false;

        part = foundPart;
        return true;
    }

    public bool AreTransplantsCompatible(EntityUid recipient, EntityUid transplant)
    {
        if (!TryComp(recipient, out TransplantCompatibilityComponent? recipientProfile) ||
            !TryComp(transplant, out TransplantCompatibilityComponent? transplantProfile))
            return false;

        return _prototypes.TryIndex(recipientProfile.Profile, out var recipientPrototype) &&
            _prototypes.TryIndex(transplantProfile.Profile, out var transplantPrototype) &&
            recipientPrototype.Accepts.Overlaps(transplantPrototype.Provides);
    }

    public bool TryCreatePartSlot(EntityUid parentId, string slot, BodyPartType type, BodyPartSymmetry symmetry)
    {
        if (string.IsNullOrWhiteSpace(slot) || !TryComp(parentId, out BodyPartComponent? parent))
            return false;

        var descriptor = new BodyPartSlot(type, symmetry);
        if (parent.ChildSlots.TryGetValue(slot, out var existing) && existing != descriptor)
            return false;

        parent.ChildSlots[slot] = descriptor;
        parent.Children[slot] = type;
        _containers.EnsureContainer<ContainerSlot>(parentId, BodyPartComponent.PartSlotPrefix + slot);
        Dirty(parentId, parent);
        return true;
    }

    public bool TryAttachPart(EntityUid parentId, string slot, EntityUid partId)
    {
        if (!TryComp(parentId, out BodyPartComponent? parent)
            || !TryComp(partId, out BodyPartComponent? part)
            || !HasComp<OrganComponent>(partId)
            || part.Body != null
            || !AreTransplantsCompatible(parentId, partId)
            || HasAmputationConsequence(parentId)
            || !parent.ChildSlots.TryGetValue(slot, out var descriptor)
            || descriptor.Type != part.PartType
            || descriptor.Symmetry != part.Symmetry)
            return false;

        if (parentId == partId || GetBodyPartChildren(partId).Any(descendant => descendant.Id == parentId))
            return false;

        var container = _containers.EnsureContainer<ContainerSlot>(parentId, BodyPartComponent.PartSlotPrefix + slot);
        if (container.ContainedEntity != null || !_containers.Insert(partId, container))
            return false;

        part.Parent = parentId;
        Dirty(partId, part);
        return true;
    }

    public bool TryCreateOrganSlot(EntityUid partId, string slot)
    {
        if (string.IsNullOrWhiteSpace(slot) || !TryComp(partId, out BodyPartComponent? part))
            return false;

        part.Organs.Add(slot);
        _containers.EnsureContainer<ContainerSlot>(partId, BodyPartComponent.OrganSlotPrefix + slot);
        Dirty(partId, part);
        return true;
    }

    public void ActivateDeclarativeGraph(EntityUid root, EntityUid body)
    {
        if (TryComp(root, out BodyPartComponent? part))
            SetSubtreeBody((root, part), body);
    }

    public IEnumerable<(EntityUid Id, OrganComponent Component)> GetPartOrgans(EntityUid part)
    {
        if (!TryComp(part, out BodyPartComponent? bodyPart))
            yield break;

        foreach (var slot in bodyPart.Organs)
        {
            if (!_containers.TryGetContainer(part, BodyPartComponent.OrganSlotPrefix + slot, out var container))
                continue;

            foreach (var organ in container.ContainedEntities)
            {
                if (TryComp(organ, out OrganComponent? component))
                    yield return (organ, component);
            }
        }
    }

    public bool TryGetOrganInSlot(EntityUid partId, string slot, out EntityUid organ)
    {
        organ = default;
        if (!_containers.TryGetContainer(partId, BodyPartComponent.OrganSlotPrefix + slot, out var container) || container is not ContainerSlot { ContainedEntity: { } contained })
            return false;

        organ = contained;
        return true;
    }

    /// <summary>
    /// Checks whether a body has an organ slot with the given category/slot name, regardless of occupancy.
    /// </summary>
    public bool HasOrganSlot(EntityUid body, string category)
    {
        foreach (var (_, part) in GetBodyChildren(body))
        {
            if (part.Organs.Contains(category))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether a body has an organ with the given category/slot name.
    /// </summary>
    public bool HasOrgan(EntityUid body, string category)
    {
        foreach (var (partId, part) in GetBodyChildren(body))
        {
            if (part.Organs.Contains(category) && TryGetOrganInSlot(partId, category, out _))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Tries to get an organ with the given category/slot name on a body.
    /// </summary>
    public bool TryGetOrgan(EntityUid body, string category, out EntityUid organ)
    {
        organ = default;
        foreach (var (partId, part) in GetBodyChildren(body))
        {
            if (!part.Organs.Contains(category))
                continue;

            if (TryGetOrganInSlot(partId, category, out organ))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Counts how many organs of the given category/slot name are present on a body.
    /// </summary>
    public int CountOrgans(EntityUid body, string category)
    {
        var count = 0;
        foreach (var (partId, part) in GetBodyChildren(body))
        {
            if (part.Organs.Contains(category) && TryGetOrganInSlot(partId, category, out _))
                count++;
        }

        return count;
    }

    public bool HasPartChild(EntityUid parentId, BodyPartType type, BodyPartSymmetry symmetry)
    {
        if (!TryComp(parentId, out BodyPartComponent? parent))
            return false;

        foreach (var slot in parent.Children.Keys)
        {
            if (!_containers.TryGetContainer(parentId, BodyPartComponent.PartSlotPrefix + slot, out var container) || container is not ContainerSlot { ContainedEntity: { } child } || !TryComp(child, out BodyPartComponent? part))
                continue;

            if (part.PartType == type && part.Symmetry == symmetry)
                return true;
        }

        return false;
    }

    private bool IsPartSlotOccupied(EntityUid parent, string slot)
    {
        return _containers.TryGetContainer(parent, BodyPartComponent.PartSlotPrefix + slot, out var container)
            && container is ContainerSlot { ContainedEntity: not null };
    }

    private static string? GetPartSlot(BodyPartComponent parent, BodyPartComponent child) => (parent.PartType, child.PartType, child.Symmetry) switch
    {
        (BodyPartType.Chest, BodyPartType.Groin, _) => "groin",
        (BodyPartType.Chest, BodyPartType.Head, _) => "head",
        (BodyPartType.Chest, BodyPartType.Arm, BodyPartSymmetry.Left) => "left_arm",
        (BodyPartType.Chest, BodyPartType.Arm, BodyPartSymmetry.Right) => "right_arm",
        (BodyPartType.Groin, BodyPartType.Leg, BodyPartSymmetry.Left) => "left_leg",
        (BodyPartType.Groin, BodyPartType.Leg, BodyPartSymmetry.Right) => "right_leg",
        (BodyPartType.Arm, BodyPartType.Hand, BodyPartSymmetry.Left) when parent.Symmetry == BodyPartSymmetry.Left => "left_hand",
        (BodyPartType.Arm, BodyPartType.Hand, BodyPartSymmetry.Right) when parent.Symmetry == BodyPartSymmetry.Right => "right_hand",
        (BodyPartType.Leg, BodyPartType.Foot, BodyPartSymmetry.Left) when parent.Symmetry == BodyPartSymmetry.Left => "left_foot",
        (BodyPartType.Leg, BodyPartType.Foot, BodyPartSymmetry.Right) when parent.Symmetry == BodyPartSymmetry.Right => "right_foot",
        _ => null,
    };

    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyPartChildren(EntityUid part)
    {
        if (!TryComp(part, out BodyPartComponent? self))
            yield break;

        yield return (part, self);

        foreach (var slot in self.Children.Keys)
        {
            if (!_containers.TryGetContainer(part, BodyPartComponent.PartSlotPrefix + slot, out var container))
                continue;

            foreach (var child in container.ContainedEntities)
            {
                if (!TryComp(child, out BodyPartComponent? childPart))
                    continue;

                foreach (var descendant in GetBodyPartChildren(child))
                    yield return descendant;
            }
        }
    }
}

/// <summary>
/// Raised on a body after its surgical organ graph has finished changing.
/// </summary>
public readonly record struct BodyOrganSlotChangedEvent(string Slot, EntityUid Organ, bool Inserted);
