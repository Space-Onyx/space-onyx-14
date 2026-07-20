using Content.Shared.Light.Components;

namespace Content.Client.Light;

public sealed partial class RoofOverlay
{
    private bool UsesProjectedRoofShadows(EntityUid gridUid, EntityUid mapUid)
    {
        if (_entManager.TryGetComponent(gridUid, out SunShadowComponent? gridSun))
            return gridSun.CastRoofShadows;

        return _entManager.TryGetComponent(mapUid, out SunShadowComponent? mapSun) &&
               mapSun.CastRoofShadows;
    }
}
