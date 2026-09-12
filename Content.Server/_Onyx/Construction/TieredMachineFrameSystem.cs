// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Server.Construction.Components;
using Content.Server.Stack;
using Content.Shared._Onyx.Construction;
using Content.Shared.Construction.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Containers;

namespace Content.Server._Onyx.Construction;

public sealed partial class TieredMachineFrameSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public bool TryInsertFromHand(Entity<MachineFrameComponent> ent, EntityUid used, EntityUid user)
    {
        if (!ent.Comp.HasBoard || !TryComp<TieredMachinePartComponent>(used, out var part))
            return false;

        if (!ent.Comp.TieredPartRequirements.TryGetValue(part.Kind, out var required)
            || ent.Comp.TieredPartProgress.GetValueOrDefault(part.Kind) >= required)
            return false;

        var inserted = used;
        if (TryComp<StackComponent>(used, out var stack) && stack.Count > 1)
        {
            if (_stack.Split((used, stack), 1, Transform(ent).Coordinates, user) is not { } split)
                return false;
            inserted = split;
        }
        else if (!_container.TryRemoveFromContainer(used))
        {
            return false;
        }

        if (!_container.Insert(inserted, ent.Comp.PartContainer))
            return false;

        ent.Comp.TieredPartProgress[part.Kind] = ent.Comp.TieredPartProgress.GetValueOrDefault(part.Kind) + 1;
        if (AreComplete(ent.Comp))
            _popup.PopupEntity(Loc.GetString("machine-frame-component-on-complete"), ent, user);
        return true;
    }

    public void AppendExamine(Entity<MachineFrameComponent> ent, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !ent.Comp.HasBoard)
            return;

        foreach (var (kind, required) in ent.Comp.TieredPartRequirements)
        {
            var missing = required - ent.Comp.TieredPartProgress.GetValueOrDefault(kind);
            if (missing <= 0)
                continue;

            args.PushMarkup(Loc.GetString("tiered-machine-frame-missing",
                ("amount", missing),
                ("kind", Loc.GetString($"tiered-machine-part-kind-{kind.ToString().ToLowerInvariant()}"))));
        }
    }

    public bool TryInsert(Entity<MachineFrameComponent> ent, EntityUid partUid, TieredMachinePartComponent part)
    {
        if (!ent.Comp.HasBoard
            || !ent.Comp.TieredPartRequirements.TryGetValue(part.Kind, out var required)
            || ent.Comp.TieredPartProgress.GetValueOrDefault(part.Kind) >= required)
            return false;

        var inserted = partUid;
        if (TryComp<StackComponent>(partUid, out var stack) && stack.Count > 1)
        {
            if (_stack.Split((partUid, stack), 1, Transform(ent).Coordinates) is not { } split)
                return false;
            inserted = split;
        }
        else if (!_container.TryRemoveFromContainer(partUid))
        {
            return false;
        }

        if (!_container.Insert(inserted, ent.Comp.PartContainer))
            return false;

        ent.Comp.TieredPartProgress[part.Kind] = ent.Comp.TieredPartProgress.GetValueOrDefault(part.Kind) + 1;
        return true;
    }

    public bool AreComplete(MachineFrameComponent component)
    {
        foreach (var (kind, required) in component.TieredPartRequirements)
        {
            if (component.TieredPartProgress.GetValueOrDefault(kind) < required)
                return false;
        }

        return true;
    }

    public void Regenerate(MachineFrameComponent component)
    {
        component.TieredPartRequirements.Clear();
        component.TieredPartProgress.Clear();
        if (!component.HasBoard)
            return;

        if (!TryComp<MachineBoardComponent>(component.BoardContainer.ContainedEntities[0], out var board))
            return;

        TieredMachinePartRequirements.CopyFromBoard(board, component.TieredPartRequirements);
        if (TieredMachinePartRequirements.ReplacesManipulators(board))
        {
            component.MaterialRequirements.Remove(TieredMachinePartRequirements.LegacyManipulator);
            component.MaterialProgress.Remove(TieredMachinePartRequirements.LegacyManipulator);
        }

        foreach (var uid in component.PartContainer.ContainedEntities)
        {
            if (!TryComp<TieredMachinePartComponent>(uid, out var part) || !component.TieredPartRequirements.ContainsKey(part.Kind))
                continue;

            var count = TryComp<StackComponent>(uid, out var stack) ? stack.Count : 1;
            component.TieredPartProgress[part.Kind] = component.TieredPartProgress.GetValueOrDefault(part.Kind) + count;
        }
    }
}
