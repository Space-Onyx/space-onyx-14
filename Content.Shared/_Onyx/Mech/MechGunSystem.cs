using Content.Shared.CCVar;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Emp;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Configuration;

namespace Content.Shared._Onyx.Mech;

/// <summary>
/// Pays installed mech weapon shots directly from the mech battery.
/// </summary>
public sealed partial class MechGunSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedMechSystem _mech = default!;
    [Dependency] private SharedGunSystem _gun = default!;

    private bool _allowOutsideMech;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechEquipmentComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<MechEquipmentComponent, GetMechWeaponChargeEvent>(OnGetCharge);
        SubscribeLocalEvent<MechEquipmentComponent, ChangeMechWeaponChargeEvent>(OnChangeCharge);
        SubscribeLocalEvent<MechEquipmentComponent, MechEquipmentInsertedEvent>(OnEquipmentChanged);
        SubscribeLocalEvent<MechEquipmentComponent, MechEquipmentRemovedEvent>(OnEquipmentChanged);
        Subs.CVar(_configuration, CCVars.MechGunOutsideMech, value => _allowOutsideMech = value, true);
    }

    private void OnShotAttempted(Entity<MechEquipmentComponent> equipment, ref ShotAttemptedEvent args)
    {
        if (!TryGetOwningMech(equipment, out var mech))
        {
            if (!_allowOutsideMech)
                args.Cancel();
            return;
        }

        if (HasComp<EmpDisabledComponent>(mech) ||
            mech.Comp.BatterySlot.ContainedEntity is not { } ||
            mech.Comp.Energy <= 0)
            args.Cancel();
    }

    private void OnGetCharge(Entity<MechEquipmentComponent> equipment, ref GetMechWeaponChargeEvent args)
    {
        if (!TryGetMechBattery(equipment, out _, out var battery))
        {
            args.Handled = !_allowOutsideMech;
            return;
        }

        args.CurrentCharge = _battery.GetCharge(battery.AsNullable());
        args.MaxCharge = battery.Comp.MaxCharge;
        args.Handled = true;
    }

    private void OnChangeCharge(Entity<MechEquipmentComponent> equipment, ref ChangeMechWeaponChargeEvent args)
    {
        if (!TryGetMechBattery(equipment, out var mech, out var battery))
        {
            args.Handled = !_allowOutsideMech;
            return;
        }

        if (args.Amount < 0 && -args.Amount > _battery.GetCharge(battery.AsNullable()))
        {
            args.Handled = true;
            return;
        }

        if (args.Amount < 0)
            _battery.UseCharge(battery.AsNullable(), -args.Amount);
        else
            _battery.ChangeCharge(battery.AsNullable(), args.Amount);
        mech.Comp.Energy = _battery.GetCharge(battery.AsNullable());
        mech.Comp.MaxEnergy = battery.Comp.MaxCharge;
        Dirty(mech);
        _mech.UpdateUserInterface(mech, mech.Comp);
        if (TryComp<BatteryAmmoProviderComponent>(equipment, out var provider))
            _gun.UpdateShots((equipment.Owner, provider));
        args.Handled = true;
        args.Changed = true;
    }

    private void OnEquipmentChanged(Entity<MechEquipmentComponent> equipment, ref MechEquipmentInsertedEvent args)
    {
        if (TryComp<BatteryAmmoProviderComponent>(equipment, out var provider))
            _gun.UpdateShots((equipment.Owner, provider));
    }

    private void OnEquipmentChanged(Entity<MechEquipmentComponent> equipment, ref MechEquipmentRemovedEvent args)
    {
        if (TryComp<BatteryAmmoProviderComponent>(equipment, out var provider))
            _gun.UpdateShots((equipment.Owner, provider));
    }

    private bool TryGetOwningMech(Entity<MechEquipmentComponent> equipment, out Entity<MechComponent> mech)
    {
        mech = default;
        if (equipment.Comp.EquipmentOwner is not { } mechUid ||
            !TryComp<MechComponent>(mechUid, out var mechComp) ||
            !mechComp.EquipmentContainer.Contains(equipment.Owner))
            return false;

        mech = (mechUid, mechComp);
        return true;
    }

    private bool TryGetMechBattery(Entity<MechEquipmentComponent> equipment,
        out Entity<MechComponent> mech,
        out Entity<BatteryComponent> battery)
    {
        mech = default;
        battery = default;
        if (!TryGetOwningMech(equipment, out mech) ||
            mech.Comp.BatterySlot.ContainedEntity is not { } batteryUid ||
            !TryComp<BatteryComponent>(batteryUid, out var batteryComp))
            return false;

        battery = (batteryUid, batteryComp);
        return true;
    }
}
