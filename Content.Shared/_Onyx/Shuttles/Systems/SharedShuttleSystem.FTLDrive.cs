using Content.Shared._Onyx.Shuttles.Components;
using Robust.Shared.Map;

namespace Content.Shared.Shuttles.Systems;

public abstract partial class SharedShuttleSystem
{
    private float GetConfiguredFTLRange(EntityUid shuttleUid)
    {
        return TryComp<FTLDriveComponent>(shuttleUid, out var drive)
            ? drive.Data.Range
            : FTLRange;
    }

    private bool CanFTLToMap(EntityUid shuttleUid, MapId currentMap, MapId targetMap)
    {
        return currentMap != targetMap ||
               TryComp<FTLDriveComponent>(shuttleUid, out var drive) && drive.Data.FTLToSameMap;
    }
}
