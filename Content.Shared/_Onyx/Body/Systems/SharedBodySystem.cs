using Content.Shared.Body.Part;
using Content.Shared.Containers;
using System.Linq;
using Robust.Shared.Containers;

namespace Content.Shared.Body.Systems;

/// <summary>
/// Surgical body-part query API. Parts are backed by Corvax external organs during the graph migration.
/// </summary>
public sealed partial class SharedBodySystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<BodyComponent, OrganInsertedIntoEvent>(OnOrganInserted);
        SubscribeLocalEvent<BodyComponent, OrganRemovedFromEvent>(OnOrganRemoved);
        SubscribeLocalEvent<BodyPartComponent, EntInsertedIntoContainerMessage>(OnPartInserted);
        SubscribeLocalEvent<BodyPartComponent, EntRemovedFromContainerMessage>(OnPartRemoved);
    }

    private void OnOrganInserted(Entity<BodyComponent> body, ref OrganInsertedIntoEvent args) => RefreshGraph(body);

    private void OnOrganRemoved(Entity<BodyComponent> body, ref OrganRemovedFromEvent args)
    {
        if (TryComp(args.Organ, out BodyPartComponent? part))
        {
            part.Body = null;
            part.Parent = null;
            Dirty(args.Organ, part);
        }

        RefreshGraph(body);
    }

    private void OnPartInserted(Entity<BodyPartComponent> parent, ref EntInsertedIntoContainerMessage args)
    {
        if (parent.Comp.Body is not { } body)
        {
            return;
        }

        if (TryComp(args.Entity, out BodyPartComponent? part))
            UpdatePartTree((args.Entity, part), body, inserted: true);
        else if (TryComp(args.Entity, out OrganComponent? organ))
            UpdateOrgan(args.Entity, organ, body, inserted: true);
    }

    private void OnPartRemoved(Entity<BodyPartComponent> parent, ref EntRemovedFromContainerMessage args)
    {
        if (parent.Comp.Body is not { } body)
        {
            return;
        }

        if (TryComp(args.Entity, out BodyPartComponent? part))
            UpdatePartTree((args.Entity, part), body, inserted: false);
        else if (TryComp(args.Entity, out OrganComponent? organ))
            UpdateOrgan(args.Entity, organ, body, inserted: false);
    }

    private void UpdatePartTree(Entity<BodyPartComponent> root, EntityUid body, bool inserted)
    {
        if (!inserted)
        {
            root.Comp.Parent = null;
            Dirty(root);
        }

        foreach (var (partId, part) in GetBodyPartChildren(root))
        {
            if (TryComp(partId, out OrganComponent? organ))
            {
                organ.Body = inserted ? body : null;
                Dirty(partId, organ);
            }

            part.Body = inserted ? body : null;
            Dirty(partId, part);

            if (inserted)
            {
                var added = new OrganGotInsertedEvent(body);
                RaiseLocalEvent(partId, ref added);
            }
            else
            {
                var removed = new OrganGotRemovedEvent(body);
                RaiseLocalEvent(partId, ref removed);
            }

            foreach (var slot in part.Organs)
            {
                if (!_containers.TryGetContainer(partId, BodyPartComponent.OrganSlotPrefix + slot, out var container))
                    continue;

                foreach (var organId in container.ContainedEntities)
                {
                    if (TryComp(organId, out OrganComponent? childOrgan))
                        UpdateOrgan(organId, childOrgan, body, inserted);
                }
            }
        }
    }

    private void UpdateOrgan(EntityUid uid, OrganComponent organ, EntityUid body, bool inserted)
    {
        if (inserted)
        {
            organ.Body = body;
            Dirty(uid, organ);
            var added = new OrganGotInsertedEvent(body);
            RaiseLocalEvent(uid, ref added);
        }
        else
        {
            organ.Body = null;
            Dirty(uid, organ);
            var removed = new OrganGotRemovedEvent(body);
            RaiseLocalEvent(uid, ref removed);
        }
    }

    private void RefreshGraph(EntityUid body)
    {
        var parts = GetBodyChildren(body).ToList();
        foreach (var (id, part) in parts)
        {
            part.Body = body;
            part.Parent = GetParent(parts, id, part);
            Dirty(id, part);
        }
    }

    private static EntityUid? GetParent(List<(EntityUid Id, BodyPartComponent Component)> parts, EntityUid id, BodyPartComponent part)
    {
        if (part.PartType is BodyPartType.Chest or BodyPartType.Torso)
            return null;

        var parentType = part.PartType switch
        {
            BodyPartType.Hand => BodyPartType.Arm,
            BodyPartType.Foot => BodyPartType.Leg,
            BodyPartType.Leg => parts.Any(candidate => candidate.Component.PartType == BodyPartType.Groin)
                ? BodyPartType.Groin
                : BodyPartType.Torso,
            BodyPartType.Groin => BodyPartType.Chest,
            _ => parts.Any(candidate => candidate.Component.PartType == BodyPartType.Chest)
                ? BodyPartType.Chest
                : BodyPartType.Torso,
        };

        foreach (var (candidate, candidatePart) in parts)
        {
            if (candidate == id || candidatePart.PartType != parentType)
                continue;

            if (parentType is BodyPartType.Arm or BodyPartType.Leg && candidatePart.Symmetry != part.Symmetry)
                continue;

            return candidate;
        }

        return null;
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

    public bool TryDetachPart(EntityUid part)
    {
        if (!TryGetParentBodyPart(part, out var parent, out var parentPart) || parent == null || parentPart == null)
            return false;

        foreach (var slot in parentPart.Children.Keys.ToList())
        {
            if (!_containers.TryGetContainer(parent.Value, BodyPartComponent.PartSlotPrefix + slot, out var container) || container is not ContainerSlot { ContainedEntity: { } child } || child != part)
                continue;

            if (!_containers.Remove(part, container))
                return false;

            parentPart.Children.Remove(slot);
            Dirty(parent.Value, parentPart);
            return true;
        }

        return false;
    }

    public bool TryAttachPart(EntityUid parentId, EntityUid partId)
    {
        if (!TryComp(parentId, out BodyPartComponent? parent) || !TryComp(partId, out BodyPartComponent? part) || part.Body != null)
            return false;

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
        if (!TryComp(partId, out BodyPartComponent? part) || !TryComp(organId, out OrganComponent? organ) || organ.Body != null)
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

    private static string? GetPartSlot(BodyPartComponent parent, BodyPartComponent child) => (parent.PartType, child.PartType, child.Symmetry) switch
    {
        (BodyPartType.Chest, BodyPartType.Groin, _) => "groin",
        (BodyPartType.Chest, BodyPartType.Head, _) => "head",
        (BodyPartType.Chest, BodyPartType.Arm, BodyPartSymmetry.Left) => "left_arm",
        (BodyPartType.Chest, BodyPartType.Arm, BodyPartSymmetry.Right) => "right_arm",
        (BodyPartType.Groin, BodyPartType.Leg, BodyPartSymmetry.Left) => "left_leg",
        (BodyPartType.Groin, BodyPartType.Leg, BodyPartSymmetry.Right) => "right_leg",
        (BodyPartType.Torso, BodyPartType.Head, _) => "head",
        (BodyPartType.Torso, BodyPartType.Arm, BodyPartSymmetry.Left) => "left_arm",
        (BodyPartType.Torso, BodyPartType.Arm, BodyPartSymmetry.Right) => "right_arm",
        (BodyPartType.Torso, BodyPartType.Leg, BodyPartSymmetry.Left) => "left_leg",
        (BodyPartType.Torso, BodyPartType.Leg, BodyPartSymmetry.Right) => "right_leg",
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
