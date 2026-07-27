using Content.Server.Mech.Systems;
using Content.Shared.Mech.Components;

namespace Content.Server.Zombies;

public sealed partial class ZombieSystem
{
    [Dependency] private MechSystem _mech = default!;

    private void EjectMechPilot(EntityUid target)
    {
        if (TryComp<MechPilotComponent>(target, out var pilot))
            _mech.TryEject(pilot.Mech, forced: true);
    }
}
