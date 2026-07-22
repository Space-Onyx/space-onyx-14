// SPDX-FileCopyrightText: 2024 Aviu00 <93730715+Aviu00@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Spatison <137375981+Spatison@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 BombasterDS <deniskaporoshok@gmail.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 SX-7 <sn1.test.preria.2002@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Power.EntitySystems;
using Content.Server._Onyx.Blocking.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;

namespace Content.Server._Onyx.Blocking.Systems;

public sealed partial class RechargeableBlockingSystem : EntitySystem
{
    [Dependency] private BatterySystem _battery = default!;
    [Dependency] private ItemToggleSystem _itemToggle = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RechargeableBlockingComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<RechargeableBlockingComponent, DamageDealtEvent>(OnDamageDealt);
        SubscribeLocalEvent<RechargeableBlockingComponent, ItemToggleActivateAttemptEvent>(AttemptToggle);
        SubscribeLocalEvent<RechargeableBlockingComponent, ChargeChangedEvent>(OnChargeChanged);
        SubscribeLocalEvent<RechargeableBlockingComponent, PowerCellChangedEvent>(OnPowerCellChanged);
    }

    private void OnExamined(Entity<RechargeableBlockingComponent> ent, ref ExaminedEvent args)
    {
        if (!ent.Comp.Discharged)
            return;

        args.PushMarkup(Loc.GetString("rechargeable-blocking-discharged"));
        args.PushMarkup(Loc.GetString("rechargeable-blocking-remaining-time", ("remainingTime", GetRemainingTime(ent.Owner))));
    }

    private int GetRemainingTime(EntityUid uid)
    {
        if (!TryComp<BatteryComponent>(uid, out var battery)
            || !TryComp<BatterySelfRechargerComponent>(uid, out var recharger)
            || recharger.AutoRechargeRate <= 0)
            return 0;

        return (int) MathF.Round((battery.MaxCharge - _battery.GetCharge((uid, battery))) / recharger.AutoRechargeRate);
    }

    private void OnDamageDealt(Entity<RechargeableBlockingComponent> ent, ref DamageDealtEvent args)
    {
        if (!TryComp<BatteryComponent>(ent.Owner, out var battery)
            || !_itemToggle.IsActivated(ent.Owner))
            return;

        var use = Math.Min(args.Damage.GetTotal().Float(), _battery.GetCharge((ent.Owner, battery)));
        _battery.UseCharge((ent.Owner, battery), use);
    }

    private void AttemptToggle(Entity<RechargeableBlockingComponent> ent, ref ItemToggleActivateAttemptEvent args)
    {
        if (!ent.Comp.Discharged)
            return;

        args.Popup = HasComp<BatterySelfRechargerComponent>(ent.Owner)
            ? Loc.GetString("rechargeable-blocking-remaining-time-popup", ("remainingTime", GetRemainingTime(ent.Owner)))
            : Loc.GetString("rechargeable-blocking-not-enough-charge-popup");
        args.Cancelled = true;
    }

    private void OnChargeChanged(Entity<RechargeableBlockingComponent> ent, ref ChargeChangedEvent args)
    {
        CheckCharge(ent);
    }

    private void OnPowerCellChanged(Entity<RechargeableBlockingComponent> ent, ref PowerCellChangedEvent args)
    {
        CheckCharge(ent);
    }

    private void CheckCharge(Entity<RechargeableBlockingComponent> ent)
    {
        if (!TryComp<BatteryComponent>(ent.Owner, out var battery))
            return;

        var charge = _battery.GetCharge((ent.Owner, battery));
        if (charge < 1)
        {
            if (TryComp<BatterySelfRechargerComponent>(ent.Owner, out var recharger))
                recharger.AutoRechargeRate = ent.Comp.DischargedRechargeRate;

            ent.Comp.Discharged = true;
            _itemToggle.TryDeactivate(ent.Owner, predicted: false);
            return;
        }

        if (MathF.Round(charge / battery.MaxCharge, 2) < ent.Comp.RechargePercentage)
            return;

        ent.Comp.Discharged = false;
        if (TryComp<BatterySelfRechargerComponent>(ent.Owner, out var chargedRecharger))
            chargedRecharger.AutoRechargeRate = ent.Comp.ChargedRechargeRate;
    }
}
