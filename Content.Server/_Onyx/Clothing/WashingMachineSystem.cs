// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Onyx.Clothing;
using Content.Shared.Audio;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Clothing.Components;
using Content.Shared.Jittering;
using Content.Shared.Lock;
using Content.Shared.Power;
using Content.Shared.Storage.Components;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._Onyx.Clothing;

public sealed partial class WashingMachineSystem : EntitySystem
{
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ClothingDirtSystem _dirt = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private LockSystem _lock = default!;
    [Dependency] private PowerReceiverSystem _power = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WashingMachineComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<WashingMachineComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<WashingMachineComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<WashingMachineComponent, ContainerIsRemovingAttemptEvent>(OnRemoveAttempt);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<WashingMachineComponent, ApcPowerReceiverComponent>();
        while (query.MoveNext(out var uid, out var washingMachine, out var power))
        {
            if (!washingMachine.IsWashing || !power.Powered)
                continue;

            washingMachine.RemainingTime -= frameTime;
            if (washingMachine.RemainingTime <= 0f)
                FinishWashing((uid, washingMachine));
        }
    }

    private void OnGetVerbs(Entity<WashingMachineComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !CanStartWashing(ent))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => TryStartWashing(ent),
            Text = Loc.GetString("washing-machine-verb-start"),
            Priority = 2,
        });
    }

    private void OnPowerChanged(Entity<WashingMachineComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered && ent.Comp.IsWashing)
            StopWashing(ent);
    }

    private void OnInsertAttempt(Entity<WashingMachineComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (ent.Comp.IsWashing && args.Container.ID == WashingMachineComponent.ContainerId)
            args.Cancel();
    }

    private void OnRemoveAttempt(Entity<WashingMachineComponent> ent, ref ContainerIsRemovingAttemptEvent args)
    {
        if (ent.Comp.IsWashing && args.Container.ID == WashingMachineComponent.ContainerId)
            args.Cancel();
    }

    public bool TryStartWashing(Entity<WashingMachineComponent> ent)
    {
        if (!CanStartWashing(ent))
            return false;

        ent.Comp.IsWashing = true;
        ent.Comp.RemainingTime = ent.Comp.WashTime;
        _lock.Lock(ent, null);
        _jitter.AddJitter(ent, -10, 100);
        _ambient.SetAmbience(ent, true);
        _appearance.SetData(ent, WashingMachineVisuals.Washing, true);
        return true;
    }

    public bool CanStartWashing(Entity<WashingMachineComponent> ent)
    {
        return !ent.Comp.IsWashing &&
               _power.IsPowered(ent) &&
               TryComp<EntityStorageComponent>(ent, out var storage) &&
               !storage.Open &&
               _container.TryGetContainer(ent, WashingMachineComponent.ContainerId, out var container) &&
               container.Count > 0;
    }

    private void FinishWashing(Entity<WashingMachineComponent> ent)
    {
        if (!_container.TryGetContainer(ent, WashingMachineComponent.ContainerId, out var container))
        {
            StopWashing(ent);
            return;
        }

        var attachedClothing = new List<Entity<AttachedClothingComponent>>();
        var attachedQuery = EntityQueryEnumerator<AttachedClothingComponent>();
        while (attachedQuery.MoveNext(out var attached, out var component))
            attachedClothing.Add((attached, component));

        foreach (var clothing in container.ContainedEntities)
        {
            _dirt.TryWashClothing(clothing, new ReagentId(ent.Comp.CleanerReagent, null), ent.Comp.WashAmount);

            foreach (var attached in attachedClothing)
            {
                if (attached.Comp.AttachedUid == clothing)
                    _dirt.TryWashClothing(attached, new ReagentId(ent.Comp.CleanerReagent, null), ent.Comp.WashAmount);
            }
        }

        _audio.PlayPvs(ent.Comp.FinishSound, ent);
        StopWashing(ent);
    }

    private void StopWashing(Entity<WashingMachineComponent> ent)
    {
        ent.Comp.IsWashing = false;
        ent.Comp.RemainingTime = 0f;
        _lock.Unlock(ent, null);
        _ambient.SetAmbience(ent, false);
        _appearance.SetData(ent, WashingMachineVisuals.Washing, false);
        RemCompDeferred<JitteringComponent>(ent);
    }
}
