// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Linq;
using Content.Shared._Onyx.Bitrunning.Components;
using Content.Shared._Onyx.PDA;
using Content.Shared._Onyx.Salvage.MiningPoints;
using Content.Shared.CartridgeLoader;
using Content.Shared.Light;
using Content.Shared.PDA;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.UserInterface;
using Robust.Shared.Containers;

namespace Content.Server.PDA;

public sealed partial class PdaSystem
{
    [Dependency] private PowerCellSystem _pdaCell = default!;
    [Dependency] private SharedBatterySystem _pdaBatterySys = default!;

    private const float PdaBatteryUiRefreshInterval = 5f;

    private readonly HashSet<EntityUid> _pdaBatteryOpen = new();
    private readonly Dictionary<EntityUid, PdaDisplaySnapshot> _pdaBatterySent = new();
    private readonly Dictionary<EntityUid, int> _pdaBatteryPercentSent = new();
    private float _pdaBatteryUiTimer;

    private void InitializePdaBattery()
    {
        SubscribeLocalEvent<PdaBatteryComponent, ComponentStartup>(OnPdaBatteryStartup);
        SubscribeLocalEvent<PdaBatteryComponent, RefreshChargeRateEvent>(OnPdaBatteryRefresh);
        SubscribeLocalEvent<PdaBatteryComponent, BoundUIOpenedEvent>(OnPdaBatteryOpened);
        SubscribeLocalEvent<PdaBatteryComponent, BoundUIClosedEvent>(OnPdaBatteryClosed);
        SubscribeLocalEvent<PdaBatteryComponent, ActivatableUIOpenAttemptEvent>(OnPdaBatteryOpenAttempt);
        SubscribeLocalEvent<PdaBatteryComponent, LightToggleEvent>(OnPdaBatteryLight);
        SubscribeLocalEvent<PdaBatteryComponent, EntInsertedIntoContainerMessage>(OnPdaBatteryCellInserted);
        SubscribeLocalEvent<PdaBatteryComponent, EntRemovedFromContainerMessage>(OnPdaBatteryCellRemoved);
        SubscribeLocalEvent<PowerCellSlotComponent, PowerCellSlotEmptyEvent>(OnPdaBatteryEmpty);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pdaBatteryOpen.Count == 0)
            return;

        // Push the charge percent the moment it changes. This only updates the
        // battery readout on the client and never touches the rest of the UI.
        foreach (var uid in _pdaBatteryOpen)
        {
            if (!TerminatingOrDeleted(uid))
                UpdateBatteryPercent(uid);
        }

        _pdaBatteryUiTimer += frameTime;
        if (_pdaBatteryUiTimer < PdaBatteryUiRefreshInterval)
            return;

        _pdaBatteryUiTimer = 0f;
        foreach (var uid in _pdaBatteryOpen.ToArray())
        {
            if (TerminatingOrDeleted(uid) || !TryComp<PdaComponent>(uid, out var pda))
            {
                _pdaBatteryOpen.Remove(uid);
                _pdaBatterySent.Remove(uid);
                _pdaBatteryPercentSent.Remove(uid);
                continue;
            }

            if (IsDisplayUnchanged(uid, pda, out var snapshot))
                continue;

            _pdaBatterySent[uid] = snapshot;
            UpdatePdaUi(uid, pda);
        }
    }

    private bool IsDisplayUnchanged(EntityUid uid, PdaComponent pda, out PdaDisplaySnapshot snapshot)
    {
        var diskUsed = 0;
        var diskMax = 0;
        if (TryComp<CartridgeLoaderComponent>(uid, out var loader))
        {
            diskUsed = _cartridgeLoader.UsedDiskSpace(uid);
            diskMax = loader.DiskSpace;
        }

        var miningPoints = CompOrNull<MiningPointsComponent>(pda.ContainedId)?.Points ?? 0;
        var bitrunningPoints = CompOrNull<BitrunningPointsComponent>(pda.ContainedId)?.Points ?? 0;
        var address = GetDeviceNetAddress(uid);

        snapshot = new PdaDisplaySnapshot(diskUsed, diskMax, miningPoints, bitrunningPoints, address);
        return _pdaBatterySent.TryGetValue(uid, out var sent) && sent.Equals(snapshot);
    }

    private readonly record struct PdaDisplaySnapshot(
        int DiskUsed,
        int DiskMax,
        int MiningPoints,
        uint BitrunningPoints,
        string? Address);

    private void UpdateBatteryPercent(EntityUid uid)
    {
        var (charge, max) = GetPdaBattery(uid);
        var percent = GetPdaBatteryPercent(charge, max);
        if (_pdaBatteryPercentSent.TryGetValue(uid, out var sent) && sent == percent)
            return;

        _pdaBatteryPercentSent[uid] = percent;
        _ui.ServerSendUiMessage(uid, PdaUiKey.Key, new PdaBatteryUpdateMessage(charge, max));
    }

    private static int GetPdaBatteryPercent(float charge, float max)
        => max > 0f ? (int) (charge / max * 100f) : -1;

    private (float Charge, float Max) GetPdaBattery(EntityUid uid)
    {
        if (!TryComp<PowerCellSlotComponent>(uid, out var slot))
            return (0f, 0f);

        if (!_pdaCell.TryGetBatteryFromSlot((uid, slot), out var battery))
            return (0f, 0f);

        return (_pdaBatterySys.GetCharge(battery.Value.AsNullable()), battery.Value.Comp.MaxCharge);
    }

    private float GetPdaLowThreshold(EntityUid uid)
        => CompOrNull<PdaBatteryComponent>(uid)?.LowThreshold ?? 0.25f;

    private void RefreshPdaBattery(EntityUid uid)
    {
        if (!TryComp<PowerCellSlotComponent>(uid, out var slot))
            return;

        if (_pdaCell.TryGetBatteryFromSlot((uid, slot), out var battery))
            _pdaBatterySys.RefreshChargeRate(battery.Value.AsNullable());
    }

    private void OnPdaBatteryRefresh(Entity<PdaBatteryComponent> ent, ref RefreshChargeRateEvent args)
    {
        var draw = ent.Comp.IdleDraw;
        if (ent.Comp.PoweredOn)
            draw += ent.Comp.PoweredDraw;
        if (TryComp<PdaComponent>(ent.Owner, out var pda) && pda.FlashlightOn)
            draw += ent.Comp.LightDraw;

        args.NewChargeRate -= draw;
    }

    private void OnPdaBatteryOpened(Entity<PdaBatteryComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!PdaUiKey.Key.Equals(args.UiKey))
            return;

        ent.Comp.PoweredOn = true;
        _pdaBatteryOpen.Add(ent.Owner);
        var (charge, max) = GetPdaBattery(ent.Owner);
        _pdaBatteryPercentSent[ent.Owner] = GetPdaBatteryPercent(charge, max);
        RefreshPdaBattery(ent.Owner);

        if (TryComp<PdaComponent>(ent.Owner, out var pda))
            UpdatePdaUi(ent.Owner, pda);
    }

    private void OnPdaBatteryClosed(Entity<PdaBatteryComponent> ent, ref BoundUIClosedEvent args)
    {
        if (!PdaUiKey.Key.Equals(args.UiKey))
            return;

        if (_ui.IsUiOpen(ent.Owner, PdaUiKey.Key))
            return;

        ent.Comp.PoweredOn = false;
        _pdaBatteryOpen.Remove(ent.Owner);
        _pdaBatterySent.Remove(ent.Owner);
        _pdaBatteryPercentSent.Remove(ent.Owner);
        RefreshPdaBattery(ent.Owner);
    }

    private void OnPdaBatteryStartup(Entity<PdaBatteryComponent> ent, ref ComponentStartup args)
    {
        RefreshPdaBattery(ent.Owner);
    }

    private void OnPdaBatteryCellInserted(Entity<PdaBatteryComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (TryComp<PowerCellSlotComponent>(ent.Owner, out var slot) && args.Container.ID == slot.CellSlotId)
            RefreshPdaBattery(ent.Owner);
    }

    private void OnPdaBatteryCellRemoved(Entity<PdaBatteryComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (TryComp<BatteryComponent>(args.Entity, out var cell))
            _pdaBatterySys.RefreshChargeRate((args.Entity, cell));
    }

    private void OnPdaBatteryOpenAttempt(Entity<PdaBatteryComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_pdaCell.HasCharge(ent.Owner, 1f, args.Silent ? null : args.User, true))
            args.Cancel();
    }

    private void OnPdaBatteryLight(Entity<PdaBatteryComponent> ent, ref LightToggleEvent args)
    {
        if (args.IsOn && !_pdaCell.HasCharge(ent.Owner, 1f))
            _unpoweredFlashlight.SetLight(ent.Owner, false);

        RefreshPdaBattery(ent.Owner);

        if (TryComp<PdaComponent>(ent.Owner, out var pda))
            UpdatePdaUi(ent.Owner, pda);
    }

    private void OnUiMessage(EntityUid uid, PdaComponent pda, PdaPowerOffMessage msg)
    {
        if (!PdaUiKey.Key.Equals(msg.UiKey))
            return;

        if (TryComp<CartridgeLoaderComponent>(uid, out var loader) && loader.ActiveProgram is { } activeProgram)
            _cartridgeLoader.DeactivateProgram((uid, loader), activeProgram);

        if (TryComp<PdaBatteryComponent>(uid, out var battery))
            battery.PoweredOn = false;

        if (TryGetPdaScreen(uid, false, out var screen))
            Appearance.SetData(uid, PdaVisuals.ScreenState, screen);

        _ui.CloseUi(uid, PdaUiKey.Key, msg.Actor);
    }

    private void OnUiMessage(EntityUid uid, PdaComponent pda, PdaToggleFlashlightMessage msg)
    {
        if (!PdaUiKey.Key.Equals(msg.UiKey))
            return;

        if (!pda.FlashlightOn && !_pdaCell.HasCharge(uid, 1f, msg.Actor, true))
            return;

        _unpoweredFlashlight.TryToggleLight(uid, user: null);
    }

    private void OnPdaBatteryEmpty(Entity<PowerCellSlotComponent> ent, ref PowerCellSlotEmptyEvent args)
    {
        if (!TryComp<PdaComponent>(ent.Owner, out var pda) || !HasComp<PdaBatteryComponent>(ent.Owner))
            return;

        if (pda.FlashlightOn)
            _unpoweredFlashlight.SetLight(ent.Owner, false);

        if (TryComp<PdaBatteryComponent>(ent.Owner, out var battery))
            battery.PoweredOn = false;

        _ui.CloseUi(ent.Owner, PdaUiKey.Key);

        if (TryGetPdaScreen(ent.Owner, false, out var screen))
            Appearance.SetData(ent.Owner, PdaVisuals.ScreenState, screen);
    }
}
