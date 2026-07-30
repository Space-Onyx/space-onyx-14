using Content.Shared._Onyx.GPS;

namespace Content.Client._Onyx.GPS;

public sealed partial class GpsSystem : SharedGpsSystem
{
    protected override bool CanTrack(Entity<GPSComponent> ent, NetEntity? trackedEntity)
    {
        return true;
    }
}
