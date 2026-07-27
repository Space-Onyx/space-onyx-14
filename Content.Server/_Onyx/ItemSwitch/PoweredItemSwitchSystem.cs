using Content.Server.Power.EntitySystems;
using Content.Shared._Onyx.ItemSwitch;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server._Onyx.ItemSwitch;

public sealed partial class PoweredItemSwitchSystem : EntitySystem
{
    [Dependency] private BatterySystem _battery = default!;
    [Dependency] private Content.Shared._Onyx.ItemSwitch.ItemSwitchSystem _switch = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ItemSwitchComponent, AttemptMeleeEvent>(OnAttemptMelee);
        SubscribeLocalEvent<ItemSwitchComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<ItemSwitchComponent, ItemSwitchAttemptEvent>(OnSwitchAttempt);
        SubscribeLocalEvent<ItemSwitchComponent, ChargeChangedEvent>(OnChargeChanged);
    }

    private void OnAttemptMelee(Entity<ItemSwitchComponent> ent, ref AttemptMeleeEvent args)
    {
        UpdatePower(ent);
    }

    private void OnMeleeHit(Entity<ItemSwitchComponent> ent, ref MeleeHitEvent args)
    {
        if (!ent.Comp.NeedsPower || !TryComp(ent, out BatteryComponent? battery) ||
            !ent.Comp.States.TryGetValue(ent.Comp.State, out var state))
            return;

        _battery.TryUseCharge((ent.Owner, battery), state.EnergyPerUse);
    }

    private void OnSwitchAttempt(Entity<ItemSwitchComponent> ent, ref ItemSwitchAttemptEvent args)
    {
        if (!ent.Comp.NeedsPower || !TryComp(ent, out BatteryComponent? battery) ||
            !ent.Comp.States.TryGetValue(args.State, out var state))
            return;

        if (_battery.GetCharge((ent.Owner, battery)) >= state.EnergyPerUse)
            return;

        args.Cancelled = true;
        if (args.User is { } user)
            _popup.PopupEntity(Loc.GetString("item-switch-failed-no-power"), ent, user);
    }

    private void OnChargeChanged(Entity<ItemSwitchComponent> ent, ref ChargeChangedEvent args)
    {
        UpdatePower(ent);
    }

    private void UpdatePower(Entity<ItemSwitchComponent> ent)
    {
        if (!ent.Comp.NeedsPower || !TryComp(ent, out BatteryComponent? battery) ||
            !ent.Comp.States.TryGetValue(ent.Comp.State, out var state))
            return;

        ent.Comp.IsPowered = _battery.GetCharge((ent.Owner, battery)) >= state.EnergyPerUse;
        if (!ent.Comp.IsPowered && ent.Comp.DefaultState is { } fallback && ent.Comp.State != fallback)
        {
            _switch.Switch(ent, fallback, predicted: false);
            ent.Comp.IsPowered = true;
        }

        Dirty(ent);
    }
}
