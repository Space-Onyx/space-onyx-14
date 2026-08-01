using Content.Shared.Mech.Components;
using Content.Shared.Vehicle.Components;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    private EntityUid ResolveMechShootingEntity(EntityUid user)
    {
        return TryComp<VehicleOperatorComponent>(user, out var pilot) &&
               pilot.Vehicle is { } vehicle &&
               HasComp<MechComponent>(vehicle)
            ? vehicle
            : user;
    }
}
