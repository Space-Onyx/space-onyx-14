using Content.Shared.ActionBlocker;
using Content.Shared.Emag.Systems;
using Content.Shared.Emp;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Movement.Events;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Toggleable;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared._Onyx.Mech;

/// <summary>
/// Handles mech policies that are independent of the upstream lifecycle.
/// </summary>
public sealed partial class MechPolicySystem : EntitySystem
{
    [Dependency] private EmagSystem _emag = default!;
    [Dependency] private SharedEmpSystem _emp = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedMechSystem _mech = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedGunSystem _gun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<MechComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<MechComponent, EmpDisabledRemovedEvent>(OnEmpDisabledRemoved);
        SubscribeLocalEvent<MechComponent, ToggleActionEvent>(OnToggleAction, before: [typeof(UnpoweredFlashlightSystem)]);
        SubscribeLocalEvent<MechComponent, AttemptPointLightToggleEvent>(OnLightToggleAttempt);
        // ComponentStartup is a single-handler [ComponentEvent] already taken by the client EmpSystem.
        SubscribeLocalEvent<EmpDisabledComponent, ComponentInit>(OnEmpDisabled);
    }

    private void OnEmagged(Entity<MechComponent> mech, ref GotEmaggedEvent args)
    {
        if (!mech.Comp.BreakOnEmag ||
            mech.Comp.EquipmentWhitelist == null ||
            !_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        mech.Comp.EquipmentWhitelist = null;
        Dirty(mech);
        args.Handled = true;
    }

    private void OnEmpPulse(Entity<MechComponent> mech, ref EmpPulseEvent args)
    {
        if (args.EnergyConsumption <= 0 ||
            mech.Comp.BatterySlot.ContainedEntity is not { } battery ||
            !TryComp<BatteryComponent>(battery, out var batteryComp))
        {
            return;
        }

        _emp.DoEmpEffects(battery, args.EnergyConsumption, args.Duration, args.User);
        mech.Comp.Energy = _battery.GetCharge((battery, batteryComp));
        mech.Comp.MaxEnergy = batteryComp.MaxCharge;
        Dirty(mech);
        _mech.UpdateUserInterface(mech, mech.Comp);
        _actionBlocker.UpdateCanMove(mech);
        CancelSelectedGun(mech);
        args.Affected = true;
        args.Disabled = true;
    }

    private void OnEmpDisabled(Entity<EmpDisabledComponent> disabled, ref ComponentInit args)
    {
        if (HasComp<MechComponent>(disabled))
            _actionBlocker.UpdateCanMove(disabled);
    }

    private void OnEmpDisabledRemoved(Entity<MechComponent> mech, ref EmpDisabledRemovedEvent args)
    {
        _actionBlocker.UpdateCanMove(mech);
    }

    private void OnToggleAction(Entity<MechComponent> mech, ref ToggleActionEvent args)
    {
        if (args.Handled ||
            mech.Comp.PilotSlot.ContainedEntity != args.Performer ||
            mech.Comp.Energy <= 0 ||
            HasComp<EmpDisabledComponent>(mech))
            args.Handled = true;
    }

    private void OnLightToggleAttempt(Entity<MechComponent> mech, ref AttemptPointLightToggleEvent args)
    {
        if (args.Enabled && mech.Comp.Energy <= 0)
            args.Cancelled = true;
    }

    private void CancelSelectedGun(Entity<MechComponent> mech)
    {
        if (mech.Comp.CurrentSelectedEquipment is { } equipment &&
            TryComp<GunComponent>(equipment, out var gun))
        {
            _gun.CancelShooting((equipment, gun));
        }
    }
}
