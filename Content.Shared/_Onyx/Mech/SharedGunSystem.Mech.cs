using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared._Onyx.Mech;
using Content.Shared.Vehicle.Components;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    private EntityUid ResolveMechShooter(EntityUid user)
    {
        return TryComp<VehicleOperatorComponent>(user, out var pilot) &&
               pilot.Vehicle is { } vehicle &&
               HasComp<MechComponent>(vehicle)
            ? vehicle
            : user;
    }

    private bool TryGetMechGun(EntityUid entity, out Entity<GunComponent> gun)
    {
        gun = default;
        if (!TryComp<VehicleOperatorComponent>(entity, out var pilot) ||
            pilot.Vehicle is not { } mechUid ||
            !TryComp<MechComponent>(mechUid, out var mech) ||
            mech.CurrentSelectedEquipment is not { } equipment ||
            !mech.EquipmentContainer.Contains(equipment) ||
            !TryComp<MechEquipmentComponent>(equipment, out var mechEquipment) ||
            mechEquipment.EquipmentOwner != mechUid ||
            !TryComp<GunComponent>(equipment, out var mechGun))
            return false;

        gun = (equipment, mechGun);
        return true;
    }

    private int? TryTakeMechCharge(Entity<BatteryAmmoProviderComponent> ent, int shots)
    {
        var ev = new ChangeMechWeaponChargeEvent(-ent.Comp.FireCost * shots);
        RaiseLocalEvent(ent, ref ev);
        return ev.Handled ? ev.Changed ? shots : 0 : null;
    }

    private (int Current, int Maximum)? TryGetMechShots(Entity<BatteryAmmoProviderComponent> ent)
    {
        var ev = new GetMechWeaponChargeEvent();
        RaiseLocalEvent(ent, ref ev);
        return ev.Handled
            ? ((int) (ev.CurrentCharge / ent.Comp.FireCost), (int) (ev.MaxCharge / ent.Comp.FireCost))
            : null;
    }

    public void CancelShooting(Entity<GunComponent> ent)
    {
        ent.Comp.ShotCounter = 0;
        ent.Comp.BurstActivated = false;
        ent.Comp.BurstShotsCount = 0;
        ent.Comp.ShootCoordinates = null;
        ent.Comp.Target = null;
        Dirty(ent);
    }
}
