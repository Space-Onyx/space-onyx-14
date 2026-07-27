using Content.Shared.Mech.Components;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    private EntityUid ResolveMechShootingEntity(EntityUid user)
    {
        return TryComp<MechPilotComponent>(user, out var pilot) ? pilot.Mech : user;
    }
}
