using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;

namespace Content.Shared._Onyx.Surgery.Augments;

public sealed partial class AugmentSystem
{
    private void InitializePower()
    {
        SubscribeLocalEvent<AugmentPowerCellSlotComponent, PowerCellChangedEvent>(OnCellChanged);
        SubscribeLocalEvent<AugmentPowerCellSlotComponent, PowerCellSlotEmptyEvent>(OnCellEmpty);
        SubscribeLocalEvent<AugmentPowerCellSlotComponent, RefreshChargeRateEvent>(OnStationRecharge);
        SubscribeLocalEvent<InsideChargerComponent, ComponentStartup>(OnEnteredCharger);
        SubscribeLocalEvent<InsideChargerComponent, ComponentShutdown>(OnLeftCharger);
    }

    private void OnCellChanged(Entity<AugmentPowerCellSlotComponent> ent, ref PowerCellChangedEvent args)
    {
        if (GetBody(ent.Owner) is not { } body)
            return;
        RefreshPower(body);
        RelayPower(body, !args.Ejected && HasPower(body));
    }

    private void OnCellEmpty(Entity<AugmentPowerCellSlotComponent> ent, ref PowerCellSlotEmptyEvent args)
    {
        if (GetBody(ent.Owner) is { } body)
            RelayPower(body, false);
    }

    private void OnStationRecharge(Entity<AugmentPowerCellSlotComponent> ent, ref RefreshChargeRateEvent args)
    {
        if (GetBody(ent.Owner) is not { } body || !HasInstalled<AugmentStationRechargerComponent>(body) ||
            !TryComp(body, out InsideChargerComponent? _) ||
            !TryComp(Transform(body).ParentUid, out ChargerComponent? charger))
            return;
        args.NewChargeRate += charger.ChargeRate;
    }

    private void OnEnteredCharger(Entity<InsideChargerComponent> ent, ref ComponentStartup args) => RefreshBattery(ent.Owner);

    private void OnLeftCharger(Entity<InsideChargerComponent> ent, ref ComponentShutdown args) => RefreshBattery(ent.Owner);

    private void RefreshBattery(EntityUid body)
    {
        if (GetPowerSlot(body) is { Valid: true } slot && _powerCell.TryGetBatteryFromSlot(slot, out var battery))
            _battery.RefreshChargeRate(battery.Value.AsNullable());
    }

    public void RefreshPower(EntityUid body)
    {
        if (GetPowerSlot(body) is not { Valid: true } slot || !TryComp(slot, out PowerCellDrawComponent? draw))
            return;
        var total = 0f;
        if (TryComp(body, out InstalledAugmentsComponent? installed))
        {
            foreach (var augment in ResolveAugments(installed))
            {
                if (TryComp(augment, out AugmentPowerDrawComponent? power) &&
                    (!HasComp<ItemToggleComponent>(augment) || _toggle.IsActivated(augment)) && IsEnabled(augment))
                    total += power.Draw;
            }
        }
        draw.DrawRate = total;
        Dirty(slot, draw);
        _powerCell.SetDrawEnabled((slot, draw), IsEnabled(slot));
        if (_powerCell.TryGetBatteryFromSlot(slot, out var battery))
            _battery.RefreshChargeRate(battery.Value.AsNullable());
    }

    private void RelayPower(EntityUid body, bool powered)
    {
        if (!TryComp(body, out InstalledAugmentsComponent? installed))
            return;
        foreach (var augment in ResolveAugments(installed))
        {
            if (!powered)
                Disable(augment);
            if (powered)
            {
                var gained = new AugmentGainedPowerEvent(body);
                RaiseLocalEvent(augment, ref gained);
            }
            else
            {
                var lost = new AugmentLostPowerEvent(body);
                RaiseLocalEvent(augment, ref lost);
            }
        }
        RefreshPower(body);
    }
}
