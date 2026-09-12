// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Linq;
using Content.Server.Beam;
using Content.Server.Construction.Components;
using Content.Shared.DoAfter;
using Content.Shared._Onyx.Construction;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Stacks;
using Content.Server.Stack;
using Content.Shared.Wires;
using Robust.Shared.Containers;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Onyx.Construction;

public sealed partial class MachinePartExchangerSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private TieredMachinePartSystem _tieredParts = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private TieredMachineFrameSystem _tieredFrame = default!;
    [Dependency] private BeamSystem _beam = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MachinePartExchangerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<MachinePartExchangerComponent, MachinePartExchangeDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(Entity<MachinePartExchangerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target || !TryComp<StorageComponent>(ent, out var storage))
            return;

        if (!HasComp<MachineComponent>(target) && !HasComp<MachineFrameComponent>(target))
            return;

        args.Handled = true;
        if (!ent.Comp.Remote && !args.CanReach)
            return;

        if (!ent.Comp.Remote && TryComp<WiresPanelComponent>(target, out var panel) && !panel.Open)
        {
            _popup.PopupEntity(Loc.GetString("tiered-machine-part-panel-closed"), target, args.User);
            return;
        }

        if (ent.Comp.BeamPrototype is { } beam)
            _beam.TryCreateBeam(args.User, target, beam);

        if (_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager,
            args.User,
            ent.Comp.ExchangeTime,
            new MachinePartExchangeDoAfterEvent(),
            ent,
            target: target,
            used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            RequireCanInteract = !ent.Comp.Remote,
            DistanceThreshold = ent.Comp.Remote ? null : 1.2f,
        }))
        {
            _audio.PlayPvs(ent.Comp.Sound, ent);
        }
    }

    private void OnDoAfter(Entity<MachinePartExchangerComponent> ent, ref MachinePartExchangeDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target is not { } target
            || !TryComp<StorageComponent>(ent, out var storage))
            return;

        TryComp<MachineComponent>(target, out var machine);
        TryComp<MachineFrameComponent>(target, out var frame);
        if (machine == null && frame == null)
            return;

        args.Handled = true;
        Exchange(ent, target, args.Args.User, storage, machine, frame);
    }

    private void Exchange(Entity<MachinePartExchangerComponent> ent,
        EntityUid target,
        EntityUid user,
        StorageComponent storage,
        MachineComponent? machine,
        MachineFrameComponent? frame)
    {

        if (frame != null)
        {
            var frameChanged = false;
            var progressed = true;
            var remainingPasses = storage.Container.ContainedEntities.Count + 1;
            while (progressed && remainingPasses-- > 0)
            {
                progressed = false;
                foreach (var uid in storage.Container.ContainedEntities.ToArray())
                {
                    var interaction = new InteractUsingEvent(user, uid, target, Transform(target).Coordinates);
                    RaiseLocalEvent(target, interaction);
                    progressed |= interaction.Handled;
                }
                frameChanged |= progressed;
            }

            _popup.PopupEntity(Loc.GetString(frameChanged
                ? "machine-part-exchanger-complete"
                : "machine-part-exchanger-nothing"), target, user);
            return;
        }

        var available = new Dictionary<MachinePartKind, List<(EntityUid Uid, TieredMachinePartComponent Part)>>();
        foreach (var uid in storage.Container.ContainedEntities)
        {
            if (!TryComp<TieredMachinePartComponent>(uid, out var part))
                continue;

            if (!available.TryGetValue(part.Kind, out var parts))
                available[part.Kind] = parts = new List<(EntityUid, TieredMachinePartComponent)>();

            var count = TryComp<StackComponent>(uid, out var stack) ? stack.Count : 1;
            for (var i = 0; i < count; i++)
                parts.Add((uid, part));
        }

        var changed = false;
        foreach (var parts in available.Values)
            parts.Sort((a, b) => a.Part.Tier.CompareTo(b.Part.Tier));

        var installed = new Dictionary<MachinePartKind, List<(EntityUid Uid, TieredMachinePartComponent Part)>>();
        foreach (var uid in machine!.PartContainer.ContainedEntities)
        {
            if (!TryComp<TieredMachinePartComponent>(uid, out var part))
                continue;

            if (!installed.TryGetValue(part.Kind, out var parts))
                installed[part.Kind] = parts = new List<(EntityUid, TieredMachinePartComponent)>();

            parts.Add((uid, part));
        }

        foreach (var (kind, currentParts) in installed)
        {
            if (!available.TryGetValue(kind, out var candidates))
                continue;

            currentParts.Sort((a, b) => a.Part.Tier.CompareTo(b.Part.Tier));
            var candidateIndex = 0;
            foreach (var current in currentParts)
            {
                while (candidateIndex < candidates.Count && candidates[candidateIndex].Part.Tier <= current.Part.Tier)
                    candidateIndex++;

                if (candidateIndex >= candidates.Count)
                    break;

                var candidate = candidates[candidateIndex++];
                var replacement = candidate.Uid;
                if (TryComp<StackComponent>(candidate.Uid, out var stack) && stack.Count > 1)
                {
                    if (_stack.Split((candidate.Uid, stack), 1, Transform(target).Coordinates, user) is not { } split)
                        continue;
                    replacement = split;
                }
                else if (!_container.TryRemoveFromContainer(candidate.Uid))
                {
                    continue;
                }

                if (!_container.Insert(replacement, machine.PartContainer))
                {
                    _storage.Insert(ent, replacement, out _, storageComp: storage, playSound: false, stackAutomatically: false);
                    continue;
                }

                _container.TryRemoveFromContainer(current.Uid);
                _storage.Insert(ent, current.Uid, out _, storageComp: storage, playSound: false, stackAutomatically: false);

                changed = true;
            }
        }

        if (!changed)
        {
            _popup.PopupEntity(Loc.GetString("machine-part-exchanger-nothing"), target, user);
            return;
        }

        _tieredParts.RefreshMachine((target, machine));
        _audio.PlayPvs(ent.Comp.Sound, ent);
        _popup.PopupEntity(Loc.GetString("machine-part-exchanger-complete"), target, user);
    }
}
