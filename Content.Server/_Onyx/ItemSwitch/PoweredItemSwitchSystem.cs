using Content.Server.Power.EntitySystems;
using Content.Shared._Onyx.ItemSwitch;
using Content.Shared.Power.Components;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server._Onyx.ItemSwitch;

public sealed partial class PoweredItemSwitchSystem : EntitySystem
{
    [Dependency] private BatterySystem _battery = default!;
    [Dependency] private Content.Shared._Onyx.ItemSwitch.ItemSwitchSystem _switch = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ItemSwitchComponent, AttemptMeleeEvent>(OnAttemptMelee);
        SubscribeLocalEvent<ItemSwitchComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnAttemptMelee(Entity<ItemSwitchComponent> ent, ref AttemptMeleeEvent args)
    {
        UpdatePower(ent);
    }

    private void OnMeleeHit(Entity<ItemSwitchComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || !ent.Comp.NeedsPower || !TryComp(ent, out BatteryComponent? battery) ||
            !ent.Comp.States.TryGetValue(ent.Comp.State, out var state))
            return;

        _battery.TryUseCharge((ent.Owner, battery), state.EnergyPerUse);
        UpdatePower(ent);
    }

    private void UpdatePower(Entity<ItemSwitchComponent> ent)
    {
        if (!ent.Comp.NeedsPower || !TryComp(ent, out BatteryComponent? battery) ||
            !ent.Comp.States.TryGetValue(ent.Comp.State, out var state))
            return;

        ent.Comp.IsPowered = _battery.GetCharge((ent.Owner, battery)) >= state.EnergyPerUse;
        if (!ent.Comp.IsPowered && ent.Comp.DefaultState is { } fallback && ent.Comp.State != fallback)
            _switch.Switch(ent, fallback, predicted: false);

        Dirty(ent);
    }
}
