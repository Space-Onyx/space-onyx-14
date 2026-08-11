using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    public void SetPuddleFlammability(EntityUid gridUid, Vector2i indices, float flammability)
    {
        if (!TryComp<GridAtmosphereComponent>(gridUid, out var atmosphere) ||
            !atmosphere.Tiles.TryGetValue(indices, out var tile) ||
            tile.PuddleSolutionFlammability == flammability)
        {
            return;
        }

        tile.PuddleSolutionFlammability = flammability;
        InvalidateVisuals(gridUid, indices);
    }

    private static float AddClampedPuddleTemperature(float temperature, float flammability)
    {
        var maximum = (float) (Atmospherics.T0C + 20 * Math.Pow(flammability, 1.2));
        return MathF.Max(temperature, MathF.Min(temperature + flammability, maximum));
    }
}
