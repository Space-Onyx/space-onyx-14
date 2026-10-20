// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body.Part;
using Robust.Shared.Containers;

namespace Content.Shared.Body;

public sealed partial class InitialBodySystem
{
    private bool InsertOrgan(EntityUid parent, string slot, EntityUid organ)
    {
        if (!_body.AreTransplantsCompatible(parent, organ))
            return false;

        var container = _container.EnsureContainer<ContainerSlot>(parent, BodyPartComponent.OrganSlotPrefix + slot);
        if (!TryComp(parent, out BodyPartComponent? part) || !_container.Insert(organ, container))
            return false;

        Comp<BodyPartComponent>(parent).Organs.Add(slot);
        Dirty(parent, part);
        return true;
    }

    private void Attach(Dictionary<BodyPartType, Dictionary<BodyPartSymmetry, EntityUid>> parts, EntityUid? parent, BodyPartType type, BodyPartSymmetry symmetry, string slot)
    {
        if (parent == null || !parts.TryGetValue(type, out var matching) || !matching.TryGetValue(symmetry, out var child) || !TryComp(parent, out BodyPartComponent? parentPart) || !TryComp(child, out BodyPartComponent? childPart))
            return;

        if (!_body.AreTransplantsCompatible(parent.Value, child))
        {
            Log.Error($"Initial body part {ToPrettyString(child)} is incompatible with parent {ToPrettyString(parent.Value)}.");
            Del(child);
            return;
        }

        var container = _container.EnsureContainer<ContainerSlot>(parent.Value, BodyPartComponent.PartSlotPrefix + slot);
        parentPart.Children[slot] = type;
        childPart.Parent = parent;
        _container.Insert(child, container);
        Dirty(parent.Value, parentPart);
        Dirty(child, childPart);
    }
}
