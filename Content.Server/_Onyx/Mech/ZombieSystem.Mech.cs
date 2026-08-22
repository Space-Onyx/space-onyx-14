using Content.Server.Mech.Systems;
using Content.Shared.Mech.Components;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle.Systems;

namespace Content.Server.Zombies;

public sealed partial class ZombieSystem
{
    [Dependency] private MechSystem _mech = default!;
    [Dependency] private VehicleSystem _vehicle = default!;

    private void EjectMechPilot(EntityUid target)
    {
        if (TryComp<VehicleOperatorComponent>(target, out var pilot) &&
            pilot.Vehicle is { } vehicle &&
            HasComp<MechComponent>(vehicle))
            _vehicle.TryExit(vehicle);
    }
}
