// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Server.Construction.Components;
using Content.Shared._Onyx.Construction;
using Content.Shared.Construction.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Wires;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Construction;

public sealed partial class TieredMachinePartSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private Content.Server.Stack.StackSystem _stack = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly Dictionary<MachinePartKind, EntProtoId> TierOneParts = new()
    {
        [MachinePartKind.Servo] = "StandardServoDrive",
        [MachinePartKind.Capacitor] = "StandardCapacitorModule",
        [MachinePartKind.MatterBin] = "StandardMatterRecycler",
        [MachinePartKind.Scanner] = "StandardScannerModule",
        [MachinePartKind.Laser] = "StandardLaserModule",
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MachineComponent, ComponentStartup>(OnMachineStartup);
        SubscribeLocalEvent<MachineComponent, InteractUsingEvent>(OnMachineInteractUsing, before: [typeof(Content.Server.Construction.MachineFrameSystem)]);
        SubscribeLocalEvent<MachineComponent, MachineUpgradeExamineEvent>(OnUpgradeExamine);
    }

    private void OnMachineStartup(Entity<MachineComponent> ent, ref ComponentStartup args)
    {
        RefreshMachine(ent);
    }

    public void EnsureBoardPartsAndRefresh(Entity<MachineComponent> ent)
    {
        EnsureBoardParts(ent);
        RefreshMachine(ent);
    }

    private void OnMachineInteractUsing(Entity<MachineComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<TieredMachinePartComponent>(args.Used, out var incoming))
            return;

        args.Handled = true;
        if (!TryComp<WiresPanelComponent>(ent, out var panel) || !panel.Open)
        {
            _popup.PopupEntity(Loc.GetString("tiered-machine-part-panel-closed"), ent, args.User);
            return;
        }

        EntityUid? currentUid = null;
        TieredMachinePartComponent? current = null;
        foreach (var uid in ent.Comp.PartContainer.ContainedEntities)
        {
            if (TryComp<TieredMachinePartComponent>(uid, out var part)
                && part.Kind == incoming.Kind
                && (current == null || part.Tier < current.Tier))
            {
                currentUid = uid;
                current = part;
            }
        }

        if (current != null && current.Tier >= incoming.Tier)
        {
            _popup.PopupEntity(Loc.GetString("tiered-machine-part-not-better"), ent, args.User);
            return;
        }

        var inserted = args.Used;
        if (TryComp<StackComponent>(args.Used, out var stack) && stack.Count > 1)
        {
            if (_stack.Split((args.Used, stack), 1, Transform(ent).Coordinates, args.User) is not { } split)
                return;
            inserted = split;
        }
        else if (!_container.TryRemoveFromContainer(args.Used))
        {
            return;
        }

        if (!_container.Insert(inserted, ent.Comp.PartContainer))
        {
            if (!_hands.TryPickupAnyHand(args.User, inserted))
                _transform.SetCoordinates(inserted, Transform(ent).Coordinates);
            return;
        }

        if (currentUid is { } replaced)
        {
            _container.TryRemoveFromContainer(replaced);
            if (!_hands.TryPickupAnyHand(args.User, replaced))
                _transform.SetCoordinates(replaced, Transform(ent).Coordinates);
        }

        RefreshMachine(ent);
        _popup.PopupEntity(Loc.GetString("tiered-machine-part-installed", ("tier", incoming.Tier)), ent, args.User);
    }

    private void OnUpgradeExamine(Entity<MachineComponent> ent, ref MachineUpgradeExamineEvent args)
    {
        foreach (var uid in ent.Comp.PartContainer.ContainedEntities)
        {
            if (!TryComp<TieredMachinePartComponent>(uid, out var part))
                continue;

            args.AddLine(Loc.GetString("tiered-machine-part-installed-examine",
                ("kind", Loc.GetString($"tiered-machine-part-kind-{part.Kind.ToString().ToLowerInvariant()}")),
                ("tier", part.Tier)));
        }
    }

    public void RefreshMachine(Entity<MachineComponent> ent)
    {
        var totals = new Dictionary<MachinePartKind, (int Tiers, int Count)>();
        foreach (var uid in ent.Comp.PartContainer.ContainedEntities)
        {
            if (!TryComp<TieredMachinePartComponent>(uid, out var part))
                continue;

            var count = TryComp<StackComponent>(uid, out var stack) ? stack.Count : 1;
            var total = totals.GetValueOrDefault(part.Kind);
            totals[part.Kind] = (total.Tiers + part.Tier * count, total.Count + count);
        }

        var ratings = new Dictionary<MachinePartKind, float>();
        var sums = new Dictionary<MachinePartKind, float>();
        foreach (var (kind, total) in totals)
        {
            ratings[kind] = (float) total.Tiers / total.Count;
            sums[kind] = total.Tiers;
        }

        RaiseLocalEvent(ent, new MachinePartsChangedEvent(ratings, sums), broadcast: true);
    }

    private void EnsureBoardParts(Entity<MachineComponent> ent)
    {
        if (ent.Comp.BoardContainer.ContainedEntities.Count == 0
            || !TryComp<MachineBoardComponent>(ent.Comp.BoardContainer.ContainedEntities[0], out var board))
            return;

        var installed = new Dictionary<MachinePartKind, int>();
        foreach (var uid in ent.Comp.PartContainer.ContainedEntities)
        {
            if (TryComp<TieredMachinePartComponent>(uid, out var part))
            {
                var count = TryComp<StackComponent>(uid, out var stack) ? stack.Count : 1;
                installed[part.Kind] = installed.GetValueOrDefault(part.Kind) + count;
            }
        }

        var requirements = new Dictionary<MachinePartKind, int>();
        TieredMachinePartRequirements.CopyFromBoard(board, requirements);
        foreach (var (kind, required) in requirements)
        {
            for (var i = installed.GetValueOrDefault(kind); i < required; i++)
            {
                var part = Spawn(TierOneParts[kind], Transform(ent).Coordinates);
                if (!_container.Insert(part, ent.Comp.PartContainer))
                    QueueDel(part);
            }
        }
    }
}
