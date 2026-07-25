using Content.Server._Onyx.Xenobiology.Equipment.Components;
using Content.Server.NPC.HTN;
using Content.Shared._Onyx.Xenobiology.Equipment.Components;
using Content.Shared.Destructible;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Timing;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._Onyx.Xenobiology.Equipment;

public sealed partial class XenovacSystem : EntitySystem
{
    private const string ReleaseDelayId = "release";
    private const string SuctionDelayId = "suction";

    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XenovacTankComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<XenovacTankComponent, DestructionEventArgs>(OnDestroyed);
        SubscribeLocalEvent<XenovacTankComponent, EntityTerminatingEvent>(OnTankTerminating);
        SubscribeLocalEvent<XenovacTankComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<XenovacComponent, GotEquippedHandEvent>(OnEquipped);
        SubscribeLocalEvent<XenovacComponent, GotUnequippedHandEvent>(OnUnequipped);
        SubscribeLocalEvent<XenovacComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnExamined(Entity<XenovacTankComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !TryGetStorage(ent, out var storage))
            return;

        args.PushMarkup(Loc.GetString("xeno-vacuum-examined", ("n", storage.Count), ("capacity", ent.Comp.Capacity)));
    }

    private void OnDestroyed(Entity<XenovacTankComponent> ent, ref DestructionEventArgs args)
    {
        Empty(ent);
    }

    private void OnTankTerminating(Entity<XenovacTankComponent> ent, ref EntityTerminatingEvent args)
    {
        Empty(ent);
    }

    private void OnRemoved(Entity<XenovacTankComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.ContainerId)
            return;

        Restore(args.Entity);
    }

    private void OnEquipped(Entity<XenovacComponent> ent, ref GotEquippedHandEvent args)
    {
        ent.Comp.LinkedTank = FindTank(args.User)?.Owner;
        Dirty(ent);
    }

    private void OnUnequipped(Entity<XenovacComponent> ent, ref GotUnequippedHandEvent args)
    {
        ent.Comp.LinkedTank = null;
        Dirty(ent);
    }

    private void OnAfterInteract(Entity<XenovacComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || IsDelayed(ent))
            return;

        var tank = ResolveTank(ent, args.User);
        if (tank == null)
        {
            _popup.PopupEntity(Loc.GetString("xeno-vacuum-suction-fail-no-tank-popup"), ent, args.User);
            return;
        }

        if (args is { Target: { } target, CanReach: true } && HasComp<MobStateComponent>(target))
        {
            if (TrySuction(ent, tank.Value, args.User, target))
                _useDelay.TryResetDelay((ent.Owner, Comp<UseDelayComponent>(ent)), false, SuctionDelayId);
            args.Handled = true;
            return;
        }

        if (!TryGetStorage(tank.Value, out var storage) || storage.Count == 0)
            return;

        var released = _containers.EmptyContainer(storage);
        foreach (var uid in released)
        {
            _popup.PopupEntity(Loc.GetString("xeno-vacuum-clear-popup", ("ent", uid)), ent, args.User);
            if (args.Target is { } destination)
                _throwing.TryThrow(uid, Transform(destination).Coordinates);
            else
                _throwing.TryThrow(uid, args.ClickLocation);
            _stun.TryUpdateParalyzeDuration(uid, TimeSpan.FromSeconds(2));
        }

        _useDelay.TryResetDelay((ent.Owner, Comp<UseDelayComponent>(ent)), false, ReleaseDelayId);
        _audio.PlayEntity(ent.Comp.ReleaseSound, ent, args.User, AudioParams.Default.WithVolume(-2f));
        args.Handled = true;
    }

    private bool TrySuction(Entity<XenovacComponent> nozzle,
        Entity<XenovacTankComponent> tank,
        EntityUid user,
        EntityUid target)
    {
        if (target == user || target == nozzle.Owner || target == tank.Owner ||
            TerminatingOrDeleted(target) || Transform(target).Anchored ||
            _containers.TryGetContainingContainer(target, out _) ||
            !_whitelist.IsWhitelistPass(nozzle.Comp.Whitelist, target))
        {
            _popup.PopupEntity(Loc.GetString("xeno-vacuum-suction-fail-invalid-entity-popup", ("ent", target)), nozzle, user);
            return false;
        }

        if (!TryGetStorage(tank, out var storage))
            return false;

        if (storage.Count >= tank.Comp.Capacity)
        {
            _popup.PopupEntity(Loc.GetString("xeno-vacuum-suction-fail-tank-full-popup"), nozzle, user);
            return false;
        }

        var captured = EnsureComp<XenovacCapturedComponent>(target);
        if (TryComp<HTNComponent>(target, out var htn))
        {
            captured.HtnWasEnabled = htn.Enabled;
            _htn.SetHTNEnabled((target, htn), false);
        }

        if (!_containers.Insert(target, storage))
        {
            Restore(target);
            return false;
        }

        _audio.PlayEntity(nozzle.Comp.SuctionSound, user, user);
        _popup.PopupEntity(Loc.GetString("xeno-vacuum-suction-succeed-popup", ("ent", target)), nozzle, user);
        return true;
    }

    private Entity<XenovacTankComponent>? ResolveTank(Entity<XenovacComponent> nozzle, EntityUid user)
    {
        if (nozzle.Comp.LinkedTank is { } linked && TryComp<XenovacTankComponent>(linked, out var tank) &&
            FindTank(user)?.Owner == linked)
            return (linked, tank);

        var found = FindTank(user);
        nozzle.Comp.LinkedTank = found?.Owner;
        Dirty(nozzle);
        return found;
    }

    private Entity<XenovacTankComponent>? FindTank(EntityUid user)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (TryComp<XenovacTankComponent>(held, out var tank))
                return (held, tank);
        }

        if (!_inventory.TryGetContainerSlotEnumerator(user, out var slots, SlotFlags.WITHOUT_POCKET))
            return null;

        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is { } uid && TryComp<XenovacTankComponent>(uid, out var tank))
                return (uid, tank);
        }

        return null;
    }

    private bool TryGetStorage(Entity<XenovacTankComponent> tank, out Container storage)
    {
        storage = _containers.EnsureContainer<Container>(tank, tank.Comp.ContainerId);
        return tank.Comp.Capacity >= 0;
    }

    private void Empty(Entity<XenovacTankComponent> tank)
    {
        if (TryGetStorage(tank, out var storage))
            _containers.EmptyContainer(storage);
    }

    private void Restore(EntityUid uid)
    {
        if (!TryComp<XenovacCapturedComponent>(uid, out var captured))
            return;

        if (captured.HtnWasEnabled && TryComp<HTNComponent>(uid, out var htn))
            _htn.SetHTNEnabled((uid, htn), true, 2f);
        RemCompDeferred<XenovacCapturedComponent>(uid);
    }

    private bool IsDelayed(Entity<XenovacComponent> nozzle)
        => _useDelay.IsDelayed(nozzle.Owner, SuctionDelayId) ||
           _useDelay.IsDelayed(nozzle.Owner, ReleaseDelayId);
}
